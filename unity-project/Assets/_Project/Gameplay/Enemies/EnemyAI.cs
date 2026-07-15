using System;
using System.Collections.Generic;
using KINETICS5.Data;
using UnityEngine;
using KINETICS5.Gameplay.Combat;

namespace KINETICS5.Gameplay.Enemies
{
    // =================================================================================
    //  BEHAVIOR TREE — Framework minimaliste (zéro dépendance externe)
    // =================================================================================

    /// <summary>Statut retourné par un nœud de behavior tree après un tick.</summary>
    public enum NodeStatus
    {
        /// <summary>Action terminée avec succès.</summary>
        Success,
        /// <summary>Action échouée ou condition non remplie.</summary>
        Failure,
        /// <summary>Action en cours, re-ticker au prochain frame.</summary>
        Running
    }

    /// <summary>
    /// Nœud de base du behavior tree. Tous les nœuds héritent de cette classe abstraite.
    /// Aucune allocation par tick : les nœuds sont construits une fois à l'initialisation.
    /// </summary>
    public abstract class BTNode
    {
        /// <summary>Exécute le nœud pour l'ennemi donné. Ne doit pas allouer.</summary>
        /// <param name="ctx">Contexte d'exécution (enemy + ai + données cache).</param>
        /// <returns>Statut du nœud.</returns>
        public abstract NodeStatus Tick(ref AIContext ctx);
    }

    /// <summary>
    /// Nœud <b>Sequence</b> : exécute les enfants dans l'ordre. S'arrête au premier
    /// <see cref="NodeStatus.Failure"/> ou <see cref="NodeStatus.Running"/> et reprend
    /// à ce point au prochain tick (mémoire d'index). Reset à 0 sur Failure.
    /// </summary>
    public sealed class Sequence : BTNode
    {
        private readonly BTNode[] _children;
        private int _runningIndex;

        public Sequence(BTNode[] children) { _children = children ?? Array.Empty<BTNode>(); }

        public override NodeStatus Tick(ref AIContext ctx)
        {
            for (; _runningIndex < _children.Length; _runningIndex++)
            {
                var status = _children[_runningIndex].Tick(ref ctx);
                if (status == NodeStatus.Running)
                {
                    // Reprendra à ce nœud au prochain tick.
                    return status;
                }
                if (status == NodeStatus.Failure)
                {
                    // Échec : la séquence entière échoue, on reset pour le prochain passage.
                    _runningIndex = 0;
                    return status;
                }
                // Success : passe au nœud suivant.
            }
            _runningIndex = 0;
            return NodeStatus.Success;
        }

        /// <summary>Réinitialise la mémoire d'index (utile pour replanification).</summary>
        public void Reset() => _runningIndex = 0;
    }

    /// <summary>
    /// Nœud <b>Selector</b> : tente les enfants dans l'ordre. Renvoie <see cref="NodeStatus.Success"/>
    /// au premier enfant qui réussit, <see cref="NodeStatus.Running"/> si un enfant est en cours,
    /// <see cref="NodeStatus.Failure"/> si tous échouent.
    /// </summary>
    public sealed class Selector : BTNode
    {
        private readonly BTNode[] _children;

        public Selector(BTNode[] children) { _children = children ?? Array.Empty<BTNode>(); }

        public override NodeStatus Tick(ref AIContext ctx)
        {
            for (int i = 0; i < _children.Length; i++)
            {
                var status = _children[i].Tick(ref ctx);
                if (status != NodeStatus.Failure)
                {
                    return status;
                }
            }
            return NodeStatus.Failure;
        }
    }

    /// <summary>
    /// Nœud <b>Action</b> : exécute une fonction lambda retournant un <see cref="NodeStatus"/>.
    /// </summary>
    public sealed class ActionNode : BTNode
    {
        private readonly AIAction _action;

        public ActionNode(AIAction action) { _action = action; }

        public override NodeStatus Tick(ref AIContext ctx) => _action(ref ctx);
    }

    /// <summary>
    /// Nœud <b>Condition</b> : exécute un prédicat. Renvoie <see cref="NodeStatus.Success"/>
    /// si vrai, <see cref="NodeStatus.Failure"/> sinon. Aucun effet de bord.
    /// </summary>
    public sealed class ConditionNode : BTNode
    {
        private readonly AIPredicate _predicate;

        public ConditionNode(AIPredicate predicate) { _predicate = predicate; }

        public override NodeStatus Tick(ref AIContext ctx) =>
            _predicate(ref ctx) ? NodeStatus.Success : NodeStatus.Failure;
    }

    /// <summary>Délégué d'action de behavior tree (zéro allocation).</summary>
    public delegate NodeStatus AIAction(ref AIContext ctx);

    /// <summary>Délégué de prédicat de behavior tree (zéro allocation).</summary>
    public delegate bool AIPredicate(ref AIContext ctx);

    /// <summary>
    /// Contexte d'exécution partagé par tous les nœuds du tick courant.
    /// Passe par <c>ref</c> pour éviter les allocations et le boxing.
    /// </summary>
    public struct AIContext
    {
        /// <summary>Contrôleur propriétaire.</summary>
        public EnemyController Enemy;
        /// <summary>IA propriétaire (pour accéder aux caches).</summary>
        public EnemyAI AI;
        /// <summary>Données ennemi résolues.</summary>
        public EnemyDto Data;
        /// <summary>Delta-temps du tick (scaled).</summary>
        public float DeltaTime;
        /// <summary>Position du joueur (snapshot en début de tick).</summary>
        public Vector3 PlayerPos;
        /// <summary>Vrai si le joueur est en vue (LOS + FOV).</summary>
        public bool PlayerInSight;
        /// <summary>Distance au joueur (mètres).</summary>
        public float PlayerDistance;
        /// <summary>Vrai si l'ennemi peut attaquer (cooldown écoulé).</summary>
        public bool CanAttack;
        /// <summary>Vitesse de mouvement calculée ce tick (pour animator).</summary>
        public float MoveSpeed;
        /// <summary>Vrai si l'ennemi est en état d'enrage (berserker bas PV).</summary>
        public bool IsEnraged;
    }

    // =================================================================================
    //  ENEMY AI — Component principal
    // =================================================================================

    /// <summary>
    /// Behavior tree léger maison (zéro dépendance externe). Construit un arbre
    /// différent par <see cref="AIBehavior"/> :
    /// <list type="bullet">
    ///   <item><b>Patrol</b> : cycle waypoints, n'attaque qu'à très courte portée.</item>
    ///   <item><b>Aggressive</b> : rush direct le joueur, tir à portée.</item>
    ///   <item><b>Defensive</b> : tient position, strafe latéral, ne poursuit pas loin.</item>
    ///   <item><b>Flanking</b> : contourne le joueur en arc de cercle.</item>
    ///   <item><b>Berserker</b> : rush comme Aggressive, vitesse x1.5 sous 50% PV, x2 sous 25%.</item>
    ///   <item><b>Sniper</b> : maintient 50-80m, recule si joueur approche.</item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// <b>Mobile-friendly :</b> un seul arbre par ennemi, ticks à fréquence réduite
    /// (définie par <see cref="EnemyController"/>), buffer de <see cref="RaycastHit"/>
    /// réutilisé, aucun <c>GameObject.Find</c>.
    /// </remarks>
    [DisallowMultipleComponent]
    public class EnemyAI : MonoBehaviour
    {
        [Header("Mouvement")]
        [Tooltip("Multiplicateur de vitesse de rotation de l'IA vers la cible de mouvement.")]
        [SerializeField] private float _moveTurnSpeed = 360f;
        [Tooltip("Distance à laquelle l'ennemi s'arrête (prévention de trembling).")]
        [SerializeField] private float _stopDistance = 0.6f;
        [Tooltip("Rayon de strafe (mouvement circulaire) en mode Flanking/Defensive.")]
        [SerializeField] private float _strafeRadius = 8f;

        [Header("Berserker")]
        [Tooltip("Seuil de PV (%) en dessous duquel le berserker s'enrage (vitesse x1.5).")]
        [Range(0f, 1f)][SerializeField] private float _enrageThreshold1 = 0.5f;
        [Tooltip("Seuil de PV (%) en dessous duquel le berserker s'enrage (vitesse x2).")]
        [Range(0f, 1f)][SerializeField] private float _enrageThreshold2 = 0.25f;

        [Header("Sniper")]
        [Tooltip("Distance de sécurité minimale du sniper (recule si joueur plus proche).")]
        [SerializeField] private float _sniperMinRange = 30f;
        [Tooltip("Distance maximale du sniper (avance si joueur plus loin).")]
        [SerializeField] private float _sniperMaxRange = 70f;

        [Header("Spatial Grid (optimisation LOS)")]
        [Tooltip("Taille d'une cellule de la grille spatiale (mètres).")]
        [SerializeField] private float _gridCellSize = 20f;

        /// <summary>Vitesse de mouvement courant (pour animator blend tree).</summary>
        public float CurrentMoveSpeed { get; private set; }

        private EnemyController _controller;
        private BTNode _root;
        private CharacterController _characterController;
        private AIContext _context;
        private float _strafeAngle;
        private bool _initialized;

        // Cache des positions waypoint.
        private Vector3[] _waypointPositions;
        private int _waypointIndex;

        private bool _isEnraged;

        /// <summary>Initialise l'IA et construit le behavior tree selon <see cref="EnemyDto.Behavior"/>.</summary>
        public void Initialize(EnemyController controller)
        {
            _controller = controller;
            _characterController = controller.GetComponent<CharacterController>();
            Data = controller.Data;
            _context = default;
            _context.Enemy = _controller;
            _context.AI = this;
            _context.Data = Data;

            // Cache waypoints (positions en Vector3 pour éviter l'accès Transform chaque tick).
            if (_controller.PatrolWaypoints != null && _controller.PatrolWaypoints.Length > 0)
            {
                _waypointPositions = new Vector3[_controller.PatrolWaypoints.Length];
                for (int i = 0; i < _waypointPositions.Length; i++)
                {
                    _waypointPositions[i] = _controller.PatrolWaypoints[i] != null
                        ? _controller.PatrolWaypoints[i].position
                        : _controller.transform.position;
                }
                _waypointIndex = 0;
            }

            // Enregistrement dans la grille spatiale (pour les requêtes allies/raycast).
            EnemySpatialGrid.Register(this);

            _root = BuildTree(Data.Behavior);
            _initialized = true;
        }

        /// <summary>Données ennemi résolues (cache local).</summary>
        public EnemyDto Data { get; private set; }

        private void OnDisable()
        {
            if (_initialized)
            {
                EnemySpatialGrid.Unregister(this);
            }
        }

        private void OnDestroy()
        {
            EnemySpatialGrid.Unregister(this);
        }

        // =================================================================================
        //  TICK EXTERNES (appelés par EnemyController par état)
        // =================================================================================

        /// <summary>Tick de patrouille : cycle waypoints.</summary>
        public void TickPatrol()
        {
            if (_waypointPositions == null || _waypointPositions.Length == 0)
            {
                CurrentMoveSpeed = 0f;
                return;
            }
            Vector3 target = _waypointPositions[_waypointIndex];
            MoveTowards(target, Data.MoveSpeed * 0.5f); // patrouille à demi-vitesse
            if (Vector3.Distance(transform.position, target) <= _controller.WaypointTolerance)
            {
                _waypointIndex = (_waypointIndex + 1) % _waypointPositions.Length;
            }
        }

        /// <summary>Tick de poursuite : dépend du comportement.</summary>
        public void TickChase()
        {
            RunRoot();
        }

        /// <summary>Tick d'attaque : repositionnement léger (strafe).</summary>
        public void TickAttack()
        {
            RunRoot();
        }

        /// <summary>Tick de fuite : s'éloigne du joueur.</summary>
        public void TickFlee()
        {
            FleeFromPlayer();
        }

        private void RunRoot()
        {
            if (!_initialized || _root == null) return;
            _context.DeltaTime = Time.deltaTime;
            _context.PlayerPos = _controller.LastKnownPlayerPosition;
            _context.PlayerInSight = _controller.HasPlayerInSight;
            _context.PlayerDistance = _controller.HasPlayerInSight
                ? Vector3.Distance(transform.position, _controller.LastKnownPlayerPosition)
                : float.MaxValue;
            _context.CanAttack = _controller.Combat != null && _controller.Combat.CanAttack;
            _context.MoveSpeed = 0f;
            _context.IsEnraged = _isEnraged;

            _root.Tick(ref _context);

            CurrentMoveSpeed = _context.MoveSpeed;
        }

        // =================================================================================
        //  CONSTRUCTION DE L'ARBRE
        // =================================================================================

        private BTNode BuildTree(AIBehavior behavior)
        {
            // Arbre commun : la spécialisation se fait dans les nœuds d'action
            // (ActMoveToTarget) qui switch sur Data.Behavior en interne.
            return new Selector(new BTNode[]
            {
                // 1. Si joueur en vue → comportement offensif.
                new Sequence(new BTNode[]
                {
                    new ConditionNode(CndPlayerInSight),
                    new Selector(new BTNode[]
                    {
                        // 1a. En range d'attaque + cooldown OK → attaquer.
                        new Sequence(new BTNode[]
                        {
                            new ConditionNode(CndInAttackRange),
                            new ConditionNode(CndCanAttack),
                            new ActionNode(ActAttack)
                        }),
                        // 1b. Trop proche pour sniper → reculer.
                        new Sequence(new BTNode[]
                        {
                            new ConditionNode(CndIsSniper),
                            new ConditionNode(CndTooCloseForSniper),
                            new ActionNode(ActFlee)
                        }),
                        // 1c. En range mais cooldown pas prêt → strafe.
                        new Sequence(new BTNode[]
                        {
                            new ConditionNode(CndInAttackRange),
                            new ActionNode(ActStrafe)
                        }),
                        // 1d. Sinon → mouvement vers cible (spécialisé par behavior).
                        new ActionNode(ActMoveToTarget)
                    })
                }),
                // 2. Si joueur repéré récemment → aller à la dernière position connue.
                new Sequence(new BTNode[]
                {
                    new ConditionNode(CndHasLastKnownPos),
                    new ActionNode(ActMoveToLastKnown)
                }),
                // 3. Sinon → patrouiller.
                new ActionNode(ActPatrol)
            });
        }

        // =================================================================================
        //  CONDITIONS
        // =================================================================================

        private bool CndPlayerInSight(ref AIContext ctx) => ctx.PlayerInSight;

        private bool CndInAttackRange(ref AIContext ctx)
        {
            float range = ctx.Data.AttackRange;
            return ctx.PlayerDistance <= range;
        }

        private bool CndCanAttack(ref AIContext ctx) => ctx.CanAttack;

        private bool CndIsSniper(ref AIContext ctx) => ctx.Data.Behavior == AIBehavior.Sniper;

        private bool CndTooCloseForSniper(ref AIContext ctx) => ctx.PlayerDistance < _sniperMinRange;

        private bool CndHasLastKnownPos(ref AIContext ctx) =>
            ctx.Enemy.LastKnownPlayerPosition != Vector3.zero;

        // =================================================================================
        //  ACTIONS
        // =================================================================================

        private NodeStatus ActAttack(ref AIContext ctx)
        {
            // L'attaque effective est déclenchée par EnemyCombat.TryAttack via EnemyController.
            // Ici on s'assure juste que l'ennemi fait face + reste quasi-immobile.
            FaceTarget(ctx.PlayerPos);
            ctx.MoveSpeed = 0f;
            return NodeStatus.Success;
        }

        private NodeStatus ActStrafe(ref AIContext ctx)
        {
            // Strafe circulaire autour du joueur (idéal pour Defensive/Flanking).
            _strafeAngle += ctx.DeltaTime * 1.2f * (Data.Behavior == AIBehavior.Flanking ? 1.5f : 1f);
            Vector3 toPlayer = ctx.PlayerPos - transform.position;
            toPlayer.y = 0f;
            float radius = _strafeRadius;
            if (toPlayer.sqrMagnitude > 0.01f)
            {
                Vector3 dir = toPlayer.normalized;
                Vector3 perp = new(-dir.z, 0f, dir.x); // perp horizontal
                Vector3 target = ctx.PlayerPos - dir * radius + perp * Mathf.Sin(_strafeAngle) * radius * 0.5f;
                target.y = transform.position.y;
                MoveTowards(target, Data.MoveSpeed * 0.7f);
            }
            ctx.MoveSpeed = Data.MoveSpeed * 0.7f;
            return NodeStatus.Running;
        }

        private NodeStatus ActFlee(ref AIContext ctx)
        {
            FleeFromPlayer();
            ctx.MoveSpeed = Data.MoveSpeed;
            return NodeStatus.Running;
        }

        private NodeStatus ActMoveToTarget(ref AIContext ctx)
        {
            // Comportement spécialisé selon Data.Behavior.
            switch (Data.Behavior)
            {
                case AIBehavior.Aggressive:
                case AIBehavior.Berserker:
                    MoveTowards(ctx.PlayerPos, GetCurrentMoveSpeed(ctx));
                    break;
                case AIBehavior.Defensive:
                    // Ne poursuit pas : strafe léger, mais reste en position.
                    if (ctx.PlayerDistance < Data.AttackRange * 0.5f)
                    {
                        FleeFromPlayer(Data.AttackRange * 0.7f);
                    }
                    else
                    {
                        // Attend en position.
                        ctx.MoveSpeed = 0f;
                    }
                    break;
                case AIBehavior.Flanking:
                    MoveFlanking(ctx.PlayerPos, GetCurrentMoveSpeed(ctx));
                    break;
                case AIBehavior.Sniper:
                    // Maintient la distance [min, max].
                    if (ctx.PlayerDistance < _sniperMinRange)
                    {
                        FleeFromPlayer();
                    }
                    else if (ctx.PlayerDistance > _sniperMaxRange)
                    {
                        MoveTowards(ctx.PlayerPos, Data.MoveSpeed * 0.6f);
                    }
                    else
                    {
                        ctx.MoveSpeed = 0f; // en position de tir
                    }
                    break;
                case AIBehavior.Patrol:
                    MoveTowards(ctx.PlayerPos, Data.MoveSpeed * 0.8f);
                    break;
            }
            return NodeStatus.Running;
        }

        private NodeStatus ActMoveToLastKnown(ref AIContext ctx)
        {
            MoveTowards(ctx.Enemy.LastKnownPlayerPosition, Data.MoveSpeed * 0.7f);
            return NodeStatus.Running;
        }

        private NodeStatus ActPatrol(ref AIContext ctx)
        {
            TickPatrol();
            return NodeStatus.Running;
        }

        // =================================================================================
        //  MOUVEMENT
        // =================================================================================

        /// <summary>Vitesse effective (avec enrage berserker si applicable).</summary>
        private float GetCurrentMoveSpeed(AIContext ctx)
        {
            float baseSpeed = Data.MoveSpeed;
            if (Data.Behavior == AIBehavior.Berserker)
            {
                float healthPct = _controller.Health != null
                    ? _controller.Health.CurrentHealth / _controller.Health.MaxHealth
                    : 1f;
                if (healthPct <= _enrageThreshold2)
                {
                    _isEnraged = true;
                    return baseSpeed * 2f;
                }
                if (healthPct <= _enrageThreshold1)
                {
                    _isEnraged = true;
                    return baseSpeed * 1.5f;
                }
                _isEnraged = false;
            }
            return baseSpeed;
        }

        private void MoveTowards(Vector3 target, float speed)
        {
            Vector3 toTarget = target - transform.position;
            toTarget.y = 0f;
            float dist = toTarget.magnitude;
            if (dist <= _stopDistance)
            {
                CurrentMoveSpeed = 0f;
                return;
            }
            Vector3 dir = toTarget / dist;
            // Rotation progressive vers la direction de mouvement.
            Quaternion targetRot = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, _moveTurnSpeed * Time.deltaTime);

            Vector3 motion = dir * (speed * Time.deltaTime);
            if (_characterController != null && _characterController.enabled)
            {
                _characterController.Move(motion);
            }
            else
            {
                transform.position += motion;
            }
            _context.MoveSpeed = speed;
        }

        private void MoveFlanking(Vector3 playerPos, float speed)
        {
            // Contourne le joueur : angle incrémental autour de la cible.
            _strafeAngle += Time.deltaTime * 0.8f;
            Vector3 toPlayer = playerPos - transform.position;
            toPlayer.y = 0f;
            float dist = toPlayer.magnitude;
            if (dist < 0.01f) return;
            Vector3 dir = toPlayer / dist;
            Vector3 perp = new(-dir.z, 0f, dir.x);
            // Position cible : décalage latéral + approche progressive.
            float targetDist = Mathf.Max(Data.AttackRange * 0.6f, dist);
            Vector3 target = playerPos - dir * targetDist + perp * Mathf.Sin(_strafeAngle) * _strafeRadius;
            target.y = transform.position.y;
            MoveTowards(target, speed);
        }

        private void FleeFromPlayer(float desiredDistance = 0f)
        {
            Vector3 fromPlayer = transform.position - _controller.LastKnownPlayerPosition;
            fromPlayer.y = 0f;
            if (fromPlayer.sqrMagnitude < 0.01f)
            {
                fromPlayer = -transform.forward;
            }
            Vector3 dir = fromPlayer.normalized;
            Vector3 target = transform.position + dir * (desiredDistance > 0f ? desiredDistance : 5f);
            target.y = transform.position.y;
            MoveTowards(target, Data.MoveSpeed * 1.2f);
        }

        private void FaceTarget(Vector3 target)
        {
            Vector3 dir = target - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) return;
            Quaternion targetRot = Quaternion.LookRotation(dir.normalized);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, _moveTurnSpeed * Time.deltaTime);
        }

        // =================================================================================
        //  SPATIAL GRID (optimisation : partage des ennemis par cellule)
        // =================================================================================

        /// <summary>
        /// Grille spatiale statique uniforme pour partitionner les ennemis par cellule.
        /// Permet aux requêtes "ennemis à proximité" (alerte alliée, AoE) de ne scanner
        /// que les cellules voisines au lieu de tous les ennemis actifs.
        /// </summary>
        internal static class EnemySpatialGrid
        {
            private static readonly Dictionary<long, HashSet<EnemyAI>> Cells = new(64);
            private static readonly List<EnemyAI> All = new(64);
            private const float CellSize = 20f;

            private static long CellKey(Vector3 p)
            {
                // Pack deux ints en long. CellSize statique (ne change pas en runtime).
                int x = Mathf.FloorToInt(p.x / CellSize);
                int z = Mathf.FloorToInt(p.z / CellSize);
                return ((long)x << 32) | (uint)z;
            }

            public static void Register(EnemyAI ai)
            {
                lock (All)
                {
                    if (!All.Contains(ai)) All.Add(ai);
                }
            }

            public static void Unregister(EnemyAI ai)
            {
                lock (All)
                {
                    All.Remove(ai);
                    // Nettoyage : on ne vide pas les cells (lazy), elles seront reconstruites au prochain Query.
                }
            }

            /// <summary>Retourne tous les ennemis dans un rayon donné autour d'une position.</summary>
            public static int QueryInRange(Vector3 center, float radius, List<EnemyAI> results)
            {
                results.Clear();
                float rSq = radius * radius;
                // Pas de rebuild de cells à chaque query : itération directe (les ennemis actifs
                // sont cappés à 12, donc O(n) reste négligeable ; la grille sert de fallback futur
                // si le cap augmente).
                lock (All)
                {
                    for (int i = 0; i < All.Count; i++)
                    {
                        var ai = All[i];
                        if (ai == null || !ai.isActiveAndEnabled) continue;
                        Vector3 p = ai.transform.position;
                        float dx = p.x - center.x;
                        float dz = p.z - center.z;
                        if (dx * dx + dz * dz <= rSq)
                        {
                            results.Add(ai);
                        }
                    }
                }
                return results.Count;
            }

            /// <summary>Compte total d'ennemis actifs (pour cap de spawner).</summary>
            public static int ActiveCount
            {
                get
                {
                    lock (All) return All.Count;
                }
            }

            /// <summary>Vide le registre (pour nettoyage de scène / changement de mission).</summary>
            public static void Clear()
            {
                lock (All)
                {
                    All.Clear();
                    Cells.Clear();
                }
            }
        }
    }
}

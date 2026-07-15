using System;
using KINETICS5.Core;
using KINETICS5.Data;
using UnityEngine;

namespace KINETICS5.Gameplay.Missions
{
    /// <summary>
    /// Suivi d'un objectif individuel de mission. Suit la progression (current/required),
    /// déclenche la complétion et publie <see cref="ObjectiveUpdatedEvent"/> sur le bus global.
    /// </summary>
    /// <remarks>
    /// <para><b>Types supportés :</b></para>
    /// <list type="bullet">
    ///   <item><b>ReachPoint</b> : atteindre une zone (trigger via <see cref="NotifyReach"/>).</item>
    ///   <item><b>KillTarget</b> : éliminer N ennemis d'un type donné (trigger via <see cref="NotifyKill"/>).</item>
    ///   <item><b>CollectItem</b> : ramasser N items (trigger via <see cref="NotifyCollect"/>).</item>
    ///   <item><b>SabotageCore</b> : détruire N cibles sabotage (trigger via <see cref="NotifySabotage"/>).</item>
    ///   <item><b>SurviveTime</b> : survivre pendant N secondes (auto-tick).</item>
    ///   <item><b>DefendPoint</b> : tenir une zone pendant N secondes (trigger via <see cref="NotifyDefendTick"/>).</item>
    ///   <item><b>Extract</b> : extraction dans une zone (trigger via <see cref="NotifyExtract"/>).</item>
    /// </list>
    /// <para>
    /// Les objectifs optionnels (bonus) ne bloquent pas la complétion de mission mais
    /// ajoutent des récompenses bonus (gérés par <see cref="MissionRewards"/>).
    /// </para>
    /// </remarks>
    [Serializable]
    public class ObjectiveTracker
    {
        /// <summary>Données de l'objectif (DTO chargé par DataLoader).</summary>
        public MissionObjectiveDto Data { get; }

        /// <summary>Progression courante.</summary>
        public int Current { get; private set; }

        /// <summary>Quantité requise.</summary>
        public int Required => Data?.RequiredCount ?? 1;

        /// <summary>Vrai si l'objectif est complété.</summary>
        public bool IsComplete { get; private set; }

        /// <summary>Vrai si l'objectif est optionnel (bonus).</summary>
        public bool IsOptional { get; set; }

        /// <summary>Vrai si l'objectif est actuellement actif (suivi en cours).</summary>
        public bool IsActive { get; set; } = true;

        /// <summary>Événement local de complétion (pour hook MissionDirector).</summary>
        public event Action<ObjectiveTracker> OnCompleted;

        /// <summary>Événement local de progression (pour UI).</summary>
        public event Action<ObjectiveTracker, int> OnProgressChanged;

        // Pour SurviveTime / DefendPoint.
        private float _timer;
        private bool _timerStarted;

        /// <summary>Constructeur à partir d'un DTO.</summary>
        public ObjectiveTracker(MissionObjectiveDto data)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            Current = 0;
            IsComplete = false;
        }

        /// <summary>Constructeur par copie (utile pour tests/clonage).</summary>
        public ObjectiveTracker(MissionObjectiveDto data, int current, bool complete)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            Current = current;
            IsComplete = complete;
        }

        // =================================================================================
        //  NOTIFY (appelés par MissionDirector ou par des triggers de scène)
        // =================================================================================

        /// <summary>Notifie qu'une zone d'atteinte a été triggerée.</summary>
        /// <param name="zoneId">Id de la zone (doit matcher Data.TargetId).</param>
        public void NotifyReach(string zoneId)
        {
            if (!IsActive || IsComplete) return;
            if (Data.Kind != ObjectiveKind.Reach) return;
            if (!string.IsNullOrEmpty(Data.TargetId) && !string.Equals(Data.TargetId, zoneId, StringComparison.OrdinalIgnoreCase))
                return;
            Complete();
        }

        /// <summary>Notifie qu'un ennemi a été tué.</summary>
        /// <param name="enemyId">Id de l'ennemi tué.</param>
        public void NotifyKill(string enemyId)
        {
            if (!IsActive || IsComplete) return;
            if (Data.Kind != ObjectiveKind.Eliminate && Data.Kind != ObjectiveKind.Assassinate) return;
            if (!string.IsNullOrEmpty(Data.TargetId) && !string.Equals(Data.TargetId, enemyId, StringComparison.OrdinalIgnoreCase))
                return;
            Increment(1);
        }

        /// <summary>Notifie qu'un item a été collecté.</summary>
        /// <param name="itemId">Id de l'item ramassé.</param>
        public void NotifyCollect(string itemId)
        {
            if (!IsActive || IsComplete) return;
            if (Data.Kind != ObjectiveKind.Collect && Data.Kind != ObjectiveKind.Scan) return;
            if (!string.IsNullOrEmpty(Data.TargetId) && !string.Equals(Data.TargetId, itemId, StringComparison.OrdinalIgnoreCase))
                return;
            Increment(1);
        }

        /// <summary>Notifie qu'une cible sabotage a été détruite.</summary>
        /// <param name="targetId">Id de la cible sabotée.</param>
        public void NotifySabotage(string targetId)
        {
            if (!IsActive || IsComplete) return;
            if (Data.Kind != ObjectiveKind.Sabotage) return;
            if (!string.IsNullOrEmpty(Data.TargetId) && !string.Equals(Data.TargetId, targetId, StringComparison.OrdinalIgnoreCase))
                return;
            Increment(1);
        }

        /// <summary>Notifie un tick de défense (appelé chaque seconde par la zone de défense).</summary>
        public void NotifyDefendTick()
        {
            if (!IsActive || IsComplete) return;
            if (Data.Kind != ObjectiveKind.Defend) return;
            Increment(1);
        }

        /// <summary>Notifie que l'extraction a été complétée.</summary>
        public void NotifyExtract()
        {
            if (!IsActive || IsComplete) return;
            if (Data.Kind != ObjectiveKind.Extract) return;
            Complete();
        }

        // =================================================================================
        //  TICK (pour SurviveTime)
        // =================================================================================

        /// <summary>Tick de l'objectif (appelé par MissionDirector.Update).</summary>
        /// <param name="deltaTime">Delta-temps en secondes.</param>
        public void Tick(float deltaTime)
        {
            if (!IsActive || IsComplete) return;
            if (Data.Kind != ObjectiveKind.Survive) return;
            if (!_timerStarted)
            {
                _timerStarted = true;
                _timer = 0f;
            }
            _timer += deltaTime;
            // Pour Survive, Required est en secondes ; on publie la progression chaque seconde.
            int newCurrent = Mathf.FloorToInt(_timer);
            if (newCurrent != Current)
            {
                Current = newCurrent;
                PublishProgress();
                if (Current >= Required)
                {
                    Complete();
                }
            }
        }

        // =================================================================================
        //  HELPERS
        // =================================================================================

        /// <summary>Incrémente la progression et publie l'événement.</summary>
        public void Increment(int amount)
        {
            if (IsComplete) return;
            Current = Mathf.Min(Required, Current + amount);
            PublishProgress();
            if (Current >= Required)
            {
                Complete();
            }
        }

        private void Complete()
        {
            if (IsComplete) return;
            IsComplete = true;
            Current = Required;
            IsActive = false;
            PublishProgress();
            try { OnCompleted?.Invoke(this); }
            catch (Exception ex) { Debug.LogError($"[ObjectiveTracker] OnCompleted handler exception: {ex}"); }
        }

        private void PublishProgress()
        {
            try { OnProgressChanged?.Invoke(this, Current); }
            catch (Exception ex) { Debug.LogError($"[ObjectiveTracker] OnProgressChanged handler exception: {ex}"); }

            // Publication zero-alloc sur le bus global.
            if (GameEventBus.Instance != null && Data != null)
            {
                GameEventBus.Instance.Publish(new ObjectiveUpdatedEvent(Data.Id, Current, Required, IsComplete));
            }
        }

        /// <summary>Représentation lisible (debug).</summary>
        public override string ToString()
        {
            string target = string.IsNullOrEmpty(Data?.TargetId) ? "*" : Data.TargetId;
            return $"[{Data?.Kind}] {Data?.Id} {Current}/{Required} (target={target}) {(IsComplete ? "DONE" : "")}";
        }
    }
}

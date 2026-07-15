using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KINETICS5.Core;

namespace KINETICS5.UI
{
    /// <summary>
    /// Atelier de craft / upgrade.
    /// </summary>
    [AddComponentMenu("KINETICS 5/Screens/CraftingScreen")]
    [DisallowMultipleComponent]
    public sealed class CraftingScreen : UIScreen
    {
        [Header("Recipes")]
        [Tooltip("Conteneur de la liste des recettes.")]
        [SerializeField] private RectTransform _recipesContainer;
        [Tooltip("Prefab d'une ligne recette.")]
        [SerializeField] private GameObject _recipeRowPrefab;

        [Header("Détails craft")]
        [Tooltip("Texte nom de l'objet à crafter.")]
        [SerializeField] private TMP_Text _craftNameText;
        [Tooltip("Texte description.")]
        [SerializeField] private TMP_Text _craftDescriptionText;
        [Tooltip("Conteneur des matériaux requis.")]
        [SerializeField] private RectTransform _materialsContainer;
        [Tooltip("Prefab d'une ligne matériau.")]
        [SerializeField] private GameObject _materialRowPrefab;
        [Tooltip("Bouton CRAFT.")]
        [SerializeField] private KButton _craftButton;

        [Header("Navigation")]
        [Tooltip("Bouton BACK.")]
        [SerializeField] private KButton _backButton;

        private readonly List<GameObject> _recipeRows = new(32);
        private readonly List<GameObject> _materialRows = new(16);
        private string _selectedRecipeId;

        /// <summary>Recette de craft.</summary>
        [System.Serializable]
        public struct CraftRecipe
        {
            public string Id;
            public string DisplayName;
            [TextArea] public string Description;
            public MaterialRequirement[] Materials;
        }

        /// <summary>Matériau requis.</summary>
        [System.Serializable]
        public struct MaterialRequirement
        {
            public string ItemId;
            public string DisplayName;
            public int Required;
            public int Owned;
        }

        [Header("Données")]
        [Tooltip("Recettes disponibles.")]
        [SerializeField] private CraftRecipe[] _recipes;

        protected override void Awake()
        {
            _screenType = ScreenType.Crafting;
            base.Awake();
        }

        protected override void InitBindings()
        {
            base.InitBindings();
            if (_craftButton != null)
            {
                _craftButton.SetLocalizationKey("crafting.craft", "CRAFT");
                _craftButton.OnKClick += _ => OnCraft();
            }
            if (_backButton != null)
            {
                _backButton.SetLocalizationKey("common.back", "BACK");
                _backButton.OnKClick += _ => OnBack();
            }
            if (_recipes == null || _recipes.Length == 0) _recipes = BuildDefaultRecipes();
        }

        protected override void OnShow(object payload)
        {
            BuildRecipeList();
            TrackClick("crafting_show");
        }

        protected override void OnHide()
        {
            ClearAll();
        }

        // =================================================================================
        //  LISTE
        // =================================================================================

        private void BuildRecipeList()
        {
            ClearAll();
            if (_recipesContainer == null || _recipeRowPrefab == null) return;
            foreach (var r in _recipes)
            {
                var row = Instantiate(_recipeRowPrefab, _recipesContainer);
                _recipeRows.Add(row);
                var texts = row.GetComponentsInChildren<TMP_Text>();
                if (texts.Length >= 2)
                {
                    texts[0].text = r.DisplayName;
                    texts[0].font = ThemeManager.Instance.GetFont(FontRole.Display);
                    texts[0].color = ThemeManager.Main;
                    texts[1].text = $"{r.Materials?.Length ?? 0} materials";
                    texts[1].font = ThemeManager.Instance.GetFont(FontRole.Mono);
                    texts[1].color = ThemeManager.TextMuted;
                }
                var btn = row.GetComponentInChildren<KButton>();
                if (btn != null)
                {
                    string id = r.Id;
                    btn.OnKClick += _ => SelectRecipe(id);
                }
            }
        }

        private void SelectRecipe(string recipeId)
        {
            _selectedRecipeId = recipeId;
            var recipe = System.Array.Find(_recipes, r => r.Id == recipeId);
            if (recipe.Id == null) return;
            if (_craftNameText != null) { _craftNameText.text = recipe.DisplayName; _craftNameText.color = ThemeManager.Main; }
            if (_craftDescriptionText != null) { _craftDescriptionText.text = recipe.Description; _craftDescriptionText.color = ThemeManager.White; }
            BuildMaterialList(recipe);
            TrackClick($"crafting_select_{recipeId}");
        }

        private void BuildMaterialList(CraftRecipe recipe)
        {
            // Nettoie anciennes lignes.
            foreach (var r in _materialRows) { if (r != null) Destroy(r); }
            _materialRows.Clear();
            if (_materialsContainer == null || _materialRowPrefab == null) return;
            if (recipe.Materials == null) return;
            foreach (var m in recipe.Materials)
            {
                var row = Instantiate(_materialRowPrefab, _materialsContainer);
                _materialRows.Add(row);
                var texts = row.GetComponentsInChildren<TMP_Text>();
                if (texts.Length >= 2)
                {
                    texts[0].text = m.DisplayName;
                    texts[0].color = ThemeManager.White;
                    texts[1].text = $"{m.Owned}/{m.Required}";
                    texts[1].color = m.Owned >= m.Required ? ThemeManager.SubGreen : ThemeManager.SubRed;
                }
            }
        }

        // =================================================================================
        //  ACTIONS
        // =================================================================================

        private void OnCraft()
        {
            if (string.IsNullOrEmpty(_selectedRecipeId)) return;
            TrackClick($"crafting_craft_{_selectedRecipeId}");
            TelemetryLogger.Instance?.Track("craft", new() { { "recipe_id", _selectedRecipeId } });
        }

        private void OnBack()
        {
            TrackClick("crafting_back");
            _ = UIManager.Instance?.ShowAsync(ScreenType.Lobby);
        }

        public override bool HandleBack() { OnBack(); return true; }

        private CraftRecipe[] BuildDefaultRecipes()
        {
            return new CraftRecipe[]
            {
                new()
                {
                    Id = "rx14_upgrade",
                    DisplayName = "HEAVY RX-14 + MK II",
                    Description = "Upgrade damage and stability of the Heavy RX-14.",
                    Materials = new MaterialRequirement[]
                    {
                        new() { ItemId = "tech_scrap", DisplayName = "Tech Scrap", Required = 50, Owned = 32 },
                        new() { ItemId = "core_module", DisplayName = "Core Module", Required = 5, Owned = 3 },
                        new() { ItemId = "alloy_plate", DisplayName = "Alloy Plate", Required = 10, Owned = 12 },
                    }
                },
                new()
                {
                    Id = "guard_v9_upgrade",
                    DisplayName = "GUARD V-9 + MK II",
                    Description = "Upgrade reload time and accuracy of the Guard V-9.",
                    Materials = new MaterialRequirement[]
                    {
                        new() { ItemId = "tech_scrap", DisplayName = "Tech Scrap", Required = 30, Owned = 32 },
                        new() { ItemId = "trigger_mod", DisplayName = "Trigger Mod", Required = 2, Owned = 0 },
                    }
                }
            };
        }

        private void ClearAll()
        {
            foreach (var r in _recipeRows) { if (r != null) Destroy(r); }
            foreach (var r in _materialRows) { if (r != null) Destroy(r); }
            _recipeRows.Clear(); _materialRows.Clear();
        }
    }
}

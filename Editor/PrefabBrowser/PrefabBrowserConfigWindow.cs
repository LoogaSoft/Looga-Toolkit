using LoogaSoft.PrefabBrowser.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace LoogaSoft.PrefabBrowser.Editor
{
    /// <summary>
    /// Edits Prefab Browser categories through retained UI Toolkit controls.
    /// </summary>
    public sealed class PrefabBrowserConfigWindow : EditorWindow
    {
        private PrefabBrowserConfig _settings;
        private ScrollView _categoryList;

        [MenuItem("Window/LoogaSoft/Prefab Browser/Browser Config")]
        public static void ShowWindow()
        {
            GetWindow<PrefabBrowserConfigWindow>("Prefab Browser Config");
        }

        private void OnEnable()
        {
            _settings = PrefabBrowserConfig.GetOrCreateConfig();
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.paddingLeft = 6f;
            root.style.paddingRight = 6f;
            root.style.paddingTop = 6f;
            root.style.paddingBottom = 6f;

            Label title = new("Category Manager");
            title.style.fontSize = 14f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 5f;
            root.Add(title);

            if (_settings == null)
            {
                root.Add(new HelpBox("Prefab Browser configuration was not found.", HelpBoxMessageType.Error));
                return;
            }

            _categoryList = new ScrollView(ScrollViewMode.Vertical);
            _categoryList.style.flexGrow = 1f;
            root.Add(_categoryList);

            Button addCategory = new(AddCategory) { text = "Add Category" };
            addCategory.style.height = 30f;
            addCategory.style.marginTop = 5f;
            root.Add(addCategory);

            RebuildCategoryList();
        }

        private void RebuildCategoryList()
        {
            if (_categoryList == null || _settings == null)
                return;

            _categoryList.Clear();
            for (int index = 0; index < _settings.Categories.Count; index++)
                _categoryList.Add(CreateCategoryElement(_settings.Categories[index], index));
        }

        private VisualElement CreateCategoryElement(BrowserCategory category, int categoryIndex)
        {
            VisualElement container = new();
            container.style.marginBottom = 5f;
            container.style.paddingLeft = 5f;
            container.style.paddingRight = 5f;
            container.style.paddingTop = 3f;
            container.style.paddingBottom = 5f;
            container.style.borderBottomWidth = 1f;
            container.style.borderLeftWidth = 1f;
            container.style.borderRightWidth = 1f;
            container.style.borderTopWidth = 1f;
            Color border = EditorGUIUtility.isProSkin
                ? new Color(0.15f, 0.15f, 0.15f)
                : new Color(0.62f, 0.62f, 0.62f);
            container.style.borderBottomColor = border;
            container.style.borderLeftColor = border;
            container.style.borderRightColor = border;
            container.style.borderTopColor = border;

            VisualElement header = new();
            header.style.flexDirection = FlexDirection.Row;

            Foldout foldout = new() { text = category.Name, value = category.IsExpanded };
            foldout.style.flexGrow = 1f;
            foldout.RegisterValueChangedCallback(evt =>
            {
                RecordChange("Change Category Foldout");
                category.IsExpanded = evt.newValue;
                MarkChanged();
            });
            header.Add(foldout);

            Button remove = new(() => RemoveCategory(categoryIndex))
            {
                text = "-",
                tooltip = "Remove this category."
            };
            remove.style.width = 26f;
            remove.style.height = 20f;
            header.Add(remove);
            container.Add(header);

            VisualElement details = new();
            details.style.paddingLeft = 14f;
            details.style.paddingTop = 3f;
            details.style.display = category.IsExpanded ? DisplayStyle.Flex : DisplayStyle.None;
            container.Add(details);
            foldout.RegisterValueChangedCallback(evt =>
                details.style.display = evt.newValue ? DisplayStyle.Flex : DisplayStyle.None);

            TextField nameField = new("Category Name") { value = category.Name };
            nameField.RegisterValueChangedCallback(evt =>
            {
                RecordChange("Rename Category");
                category.Name = evt.newValue;
                foldout.text = evt.newValue;
                MarkChanged();
            });
            details.Add(nameField);

            Label subcategoryTitle = new("Subcategories");
            subcategoryTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            subcategoryTitle.style.marginTop = 5f;
            subcategoryTitle.style.marginBottom = 2f;
            details.Add(subcategoryTitle);

            for (int subcategoryIndex = 0; subcategoryIndex < category.SubCategories.Count; subcategoryIndex++)
                details.Add(CreateSubcategoryRow(category, subcategoryIndex));

            Button addSubcategory = new(() => AddSubcategory(category)) { text = "Add Subcategory" };
            addSubcategory.style.marginTop = 3f;
            details.Add(addSubcategory);
            return container;
        }

        private VisualElement CreateSubcategoryRow(BrowserCategory category, int subcategoryIndex)
        {
            VisualElement row = new();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginBottom = 2f;

            TextField field = new() { value = category.SubCategories[subcategoryIndex] };
            field.style.flexGrow = 1f;
            field.RegisterValueChangedCallback(evt =>
            {
                RecordChange("Rename Subcategory");
                category.SubCategories[subcategoryIndex] = evt.newValue;
                MarkChanged();
            });
            row.Add(field);

            Button remove = new(() => RemoveSubcategory(category, subcategoryIndex))
            {
                text = "-",
                tooltip = "Remove this subcategory."
            };
            remove.style.width = 26f;
            remove.style.marginLeft = 3f;
            row.Add(remove);
            return row;
        }

        private void AddCategory()
        {
            RecordChange("Add Category");
            _settings.Categories.Add(new BrowserCategory { Name = "New Category" });
            MarkChanged();
            ScheduleRebuild();
        }

        private void RemoveCategory(int index)
        {
            if (index < 0 || index >= _settings.Categories.Count)
                return;

            RecordChange("Remove Category");
            _settings.Categories.RemoveAt(index);
            MarkChanged();
            ScheduleRebuild();
        }

        private void AddSubcategory(BrowserCategory category)
        {
            RecordChange("Add Subcategory");
            category.SubCategories.Add("New Subcategory");
            MarkChanged();
            ScheduleRebuild();
        }

        private void RemoveSubcategory(BrowserCategory category, int index)
        {
            if (index < 0 || index >= category.SubCategories.Count)
                return;

            RecordChange("Remove Subcategory");
            category.SubCategories.RemoveAt(index);
            MarkChanged();
            ScheduleRebuild();
        }

        private void ScheduleRebuild()
        {
            rootVisualElement.schedule.Execute(RebuildCategoryList);
        }

        private void RecordChange(string undoName)
        {
            Undo.RecordObject(_settings, undoName);
        }

        private void MarkChanged()
        {
            EditorUtility.SetDirty(_settings);
        }
    }
}

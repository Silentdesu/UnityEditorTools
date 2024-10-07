using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace EditorTools.MaterialFinder
{
    public sealed class MaterialFinderEditorWindow : EditorWindow
    {
        [SerializeField] private VisualTreeAsset _visualTreeAsset;

        private Button _convertButton;

        private List<string> _desirableChoices = new();
        private DropdownField _desirableTypeDropdown;
        private DropdownField _searchableTypeDropdown;
        private Foldout _foldout;
        private Button _gpuInstancingButton;
        private Toggle _gpuInstancingToggle;
        private Label _label;
        private ListView _listView;

        private List<Material> _materials;
        private Toggle _projectMaterialsToggle;

        private MaterialFinderConfig _config;

        private const string TOOL_NAME = "Material Finder";
        private const string SEARCHABLE_TYPE_DROPDOWN = "searchable-type";
        private const string DESIRABLE_TYPE_DROPDOWN = "desirable-type";
        private const string PROJECT_MATERIALS_TOGGLE = "project-materials-toggle";
        private const string GPU_INSTANCING_TOGGLE = "gpu-instancing-toggle";
        private const string FOLDOUT = "foldout";
        private const string LIST_VIEW = "listview";
        private const string LABEL = "label";
        private const string CONVERT_BUTTON = "convert-button";
        private const string GPU_INSTANCING_BUTTOn = "gpu-instancing-button";

        private const string PACKAGES = "Packages";
        private const string PROJECT_MATERIALS = "Project Materials";
        private const string ADDRESSABLES_MATERIALS = "Addressables Materials";
        private const string FILTER = "t: Material";
        private const string SEARCHABLE_FOLDER = "Assets";

        [MenuItem(Constants.TOOLS_PATH + TOOL_NAME)]
        public static void ShowEditorWindow()
        {
            var window = GetWindow<MaterialFinderEditorWindow>();
            window.titleContent = new GUIContent(TOOL_NAME);
            window.Show();
        }

        private void OnEnable()
        {
            _config = MaterialFinderConfig.instance;

            if (_config == null)
            {
                Debug.LogError("Config is null");
                return;
            }
            
            Initialize();
            GetMaterials(_config.Shaders[0], _projectMaterialsToggle.value);
            UpdateListView(ref _listView, FOLDOUT);
            UpdateLabel(_label, $"Found {_config.Shaders[0]} [{_materials.Count.ToString()}]");
            UpdateDesirableChoices(_config.Shaders[0]);
            Debug.Log($"[{nameof(MaterialFinderEditorWindow)}] has opened");
        }

        private void OnDisable()
        {
            UnsubscribeButton(_convertButton, OnConvertButtonClicked);
            UnsubscribeButton(_gpuInstancingButton, OnGPUInstancingButtonClicked);

            void UnsubscribeButton(Button button, Action callback)
            {
                if (button == null)
                {
                    return;
                }

                button.clicked -= callback;
            }
        }

        private void Initialize()
        {
            _materials = new List<Material>();

            _visualTreeAsset.CloneTree(rootVisualElement);
            _searchableTypeDropdown = rootVisualElement.Q<DropdownField>(SEARCHABLE_TYPE_DROPDOWN);
            _desirableTypeDropdown = rootVisualElement.Q<DropdownField>(DESIRABLE_TYPE_DROPDOWN);
            _projectMaterialsToggle = rootVisualElement.Q<Toggle>(PROJECT_MATERIALS_TOGGLE);
            _gpuInstancingToggle = rootVisualElement.Q<Toggle>(GPU_INSTANCING_TOGGLE);
            _foldout = rootVisualElement.Q<Foldout>(FOLDOUT);
            _listView = rootVisualElement.Q<ListView>(LIST_VIEW);
            _label = rootVisualElement.Q<Label>(LABEL);
            _convertButton = rootVisualElement.Q<Button>(CONVERT_BUTTON);
            _gpuInstancingButton = rootVisualElement.Q<Button>(GPU_INSTANCING_BUTTOn);

            _convertButton?.SetEnabled(false);
            SubscribeButton(_convertButton, OnConvertButtonClicked);
            SubscribeButton(_gpuInstancingButton, OnGPUInstancingButtonClicked);

            _searchableTypeDropdown.choices = _config.Shaders.ToList();
            _searchableTypeDropdown.value = _config.DefaultShader;
            _searchableTypeDropdown.RegisterValueChangedCallback(choice =>
            {
                RefreshWindow(choice.newValue, _projectMaterialsToggle.value);
                UpdateDesirableChoices(choice.newValue);
            });

            _desirableTypeDropdown.RegisterValueChangedCallback(choice =>
            {
                _convertButton.SetEnabled(!string.IsNullOrEmpty(choice.newValue) &&
                                          _materials.Count != 0);
            });

            _projectMaterialsToggle.RegisterValueChangedCallback(choice =>
            {
                RefreshWindow(_searchableTypeDropdown.value, choice.newValue);
                _foldout.text = choice.newValue ? PROJECT_MATERIALS : ADDRESSABLES_MATERIALS;
            });

            void SubscribeButton(Button button, Action callback)
            {
                if (button == null)
                {
                    return;
                }

                button.clicked += callback;
            }
        }

        private void RefreshWindow(string shaderName, bool isProjectMaterials)
        {
            GetMaterials(shaderName, isProjectMaterials);
            _listView.RefreshItems();
            UpdateLabel(_label, $"Found {shaderName} [{_materials.Count.ToString()}]");
        }

        private void GetMaterials(string shaderName, bool isProjectMaterials)
        {
            if (isProjectMaterials) GetProjectMaterials(ref _materials, shaderName);
            else GetAddressablesMaterials(ref _materials, shaderName);
        }

        private void GetAddressablesMaterials(ref List<Material> materials, string shaderName)
        {
            var group = AddressablesUtils.GetOrCreateGroup("Materials");

            if (group == null)
            {
                Debug.LogError("Couldn't find group");
                return;
            }

            var temp = new List<Material>(group.entries.Count);

            foreach (var entry in group.entries)
            {
                if (!(entry.MainAsset is Material material && !entry.AssetPath.Contains(PACKAGES))) continue;
                temp.Add(material);
            }

            temp = temp.OrderBy(m => m.name).ToList();
            materials.Clear();

            foreach (var material in temp)
            {
                if (!material.shader.name.Equals(shaderName)) continue;
                materials.Add(material);
            }
        }

        private void GetProjectMaterials(ref List<Material> materials, string shaderName)
        {
            var materialPaths = AssetDatabase.FindAssets(FILTER, new[] { SEARCHABLE_FOLDER })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(s => s.Split('/')[^1])
                .ToList();

            materials.Clear();

            foreach (var path in materialPaths)
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!(material.shader.name.Equals(shaderName) && !path.Contains(PACKAGES) &&
                      !path.ToLower().Contains(".fbx"))) continue;

                materials.Add(material);
            }
        }

        private void UpdateListView(ref ListView listView, string rootName)
        {
            Func<VisualElement> makeItem = () => new ObjectField();

            Action<VisualElement, int> bindItem = (e, i) =>
            {
                var objectField = e as ObjectField;
                objectField.objectType = typeof(Material);
                objectField.allowSceneObjects = false;
                objectField.value = _materials[i];
            };

            listView.makeItem = makeItem;
            listView.bindItem = bindItem;
            listView.itemsSource = _materials;
            listView.fixedItemHeight = 16.0f;
            listView.style.flexGrow = 1.0f;
        }

        private void UpdateLabel(Label label, string newText)
        {
            label.text = newText;
        }

        private void UpdateDesirableChoices(string removableChoice)
        {
            _desirableChoices = new List<string>(_config.Shaders);
            _desirableChoices.Remove(removableChoice);
            _desirableTypeDropdown.choices = _desirableChoices;
            _desirableTypeDropdown.value = "";
            _convertButton.SetEnabled(false);
        }

        private void OnConvertButtonClicked()
        {
            foreach (var material in _materials)
            {
                material.shader = Shader.Find(_desirableTypeDropdown.value);
            }

            RefreshWindow(_config.Shaders[0], _projectMaterialsToggle.value);
            _desirableTypeDropdown.value = "";
            EditorApplication.ExecuteMenuItem("File/Save Project");
        }

        private void OnGPUInstancingButtonClicked()
        {
            foreach (var material in _materials)
            {
                material.enableInstancing = _gpuInstancingToggle.value;
            }
        }
    }
}
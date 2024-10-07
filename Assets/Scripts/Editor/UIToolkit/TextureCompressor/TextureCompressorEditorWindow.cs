using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace EditorTools.TextureCompressor
{
    public class TextureCompressorEditorWindow : EditorWindow
    {
        [SerializeField] private VisualTreeAsset _visualTreeAsset;

        private Button _folderPathCopyButton;
        private TextField _folderPathTextField;
        private Button _diffuseMapButton;
        private Button _normalMapButton;
        private ListView _textureListView;
        private Toggle _generateMipmapsToggle;
        private Toggle _mipStreamingToggle;
        private DropdownField _maxSizeDropdownField;
        private DropdownField _compressionDropdownField;
        private Toggle _useCrunchCompressionToggle;
        private IMGUIContainer _textureIMGUI;
        private Label _textureLabel;
        private Button _applyButton;

        private string _path;
        private List<Texture2D> _textures = new();

        private readonly Dictionary<string, int> _resolutionMap = new()
        {
            { "32x32", 32 },
            { "64x64", 64 },
            { "128x128", 128 },
            { "256x256", 256 },
            { "512x512", 512 },
            { "1024x1024", 1024 },
            { "2048x2048", 2048 },
            { "4096x4096", 4096 }
        };

        private readonly Dictionary<string, TextureImporterCompression> _compressionMap = new()
        {
            { "None", TextureImporterCompression.Uncompressed },
            { "Low Quality", TextureImporterCompression.CompressedLQ },
            { "Mid Quality", TextureImporterCompression.Compressed },
            { "High Quality", TextureImporterCompression.CompressedHQ }
        };

        private static readonly Vector2 WINDOW_MAX_SIZE = new Vector2(400, 700);
        
        private const string TOOL_NAME = "Texture Compressor";
        private const string FOLDER_PATH_TEXT_FIELD = "folder-path-text-field";
        private const string FOLDER_PATH_COPY_BUTTON = "copy-path-button";
        private const string DIFFUSE_MAP_BUTTON = "diffuse-map-button";
        private const string NORMAL_MAP_BUTTOn = "normal-map-button";
        private const string TEXTURE_LIST_VIEW = "textures-list-view";
        private const string GENERATE_MIPMAPS_TOGGLE = "generate-mipmaps-toggle";
        private const string MIP_STREAMING_TOGGLE = "mip-streaming-toggle";
        private const string MAX_SIZE_DROPDOWN = "max-size-dropdown";
        private const string COMPRESSION_DROPDOWN = "compression-dropdown";
        private const string USE_CRUNCH_COMPRESSION_TOGGLE = "use-crunch-compression-toggle";
        private const string TEXTURE_IMGUI = "texture-imgui";
        private const string TEXTURE_LABEL = "texture-label";
        private const string APPLY_BUTTON = "apply-button";

        private const string MAX_SIZE_DEFAULT_VALUE = "512x512";
        private const string COMPRESSION_DEFAULT_VALUE = "High Quality";


        [MenuItem(Constants.TOOLS_PATH + TOOL_NAME)]
        private static void ShowEditorWindow()
        {
            var window = GetWindow<TextureCompressorEditorWindow>(utility: true, TOOL_NAME, focus: true);
            window.minSize = window.maxSize = WINDOW_MAX_SIZE;
            window.Show();
        }

        private void OnEnable()
        {
            Init();

            void Init()
            {
                _visualTreeAsset.CloneTree(rootVisualElement);

                _folderPathCopyButton = rootVisualElement.Q<Button>(FOLDER_PATH_COPY_BUTTON);
                _folderPathTextField = rootVisualElement.Q<TextField>(FOLDER_PATH_TEXT_FIELD);
                _diffuseMapButton = rootVisualElement.Q<Button>(DIFFUSE_MAP_BUTTON);
                _normalMapButton = rootVisualElement.Q<Button>(NORMAL_MAP_BUTTOn);
                _textureListView = rootVisualElement.Q<ListView>(TEXTURE_LIST_VIEW);
                _generateMipmapsToggle = rootVisualElement.Q<Toggle>(GENERATE_MIPMAPS_TOGGLE);
                _mipStreamingToggle = rootVisualElement.Q<Toggle>(MIP_STREAMING_TOGGLE);
                _maxSizeDropdownField = rootVisualElement.Q<DropdownField>(MAX_SIZE_DROPDOWN);
                _compressionDropdownField = rootVisualElement.Q<DropdownField>(COMPRESSION_DROPDOWN);
                _useCrunchCompressionToggle = rootVisualElement.Q<Toggle>(USE_CRUNCH_COMPRESSION_TOGGLE);
                _textureIMGUI = rootVisualElement.Q<IMGUIContainer>(TEXTURE_IMGUI);
                _textureLabel = rootVisualElement.Q<Label>(TEXTURE_LABEL);
                _applyButton = rootVisualElement.Q<Button>(APPLY_BUTTON);

                FillDropdown(_maxSizeDropdownField, _resolutionMap.Select(e => e.Key).ToList(), MAX_SIZE_DEFAULT_VALUE);
                FillDropdown(_compressionDropdownField, _compressionMap.Select(e => e.Key).ToList(),
                    COMPRESSION_DEFAULT_VALUE);
                SubscribeButton(_folderPathCopyButton, OnCopyButtonClicked);
                SubscribeButton(_diffuseMapButton, OnDiffuseMapButtonClicked);
                SubscribeButton(_normalMapButton, OnNormalMapButtonClicked);
                SubscribeButton(_applyButton, OnApplyButtonClicked);

                _applyButton.SetEnabled(false);
            }

            void FillDropdown(DropdownField dropdownField, List<string> choices, string defaultValue)
            {
                if (dropdownField == null)
                {
                    return;
                }

                dropdownField.choices = new List<string>(choices);
                dropdownField.value = choices.FirstOrDefault(s => s.Equals(defaultValue));
            }

            void SubscribeButton(Button button, Action callback)
            {
                if (button == null)
                {
                    return;
                }

                button.clicked += callback;
            }
        }

        private void OnDisable()
        {
            UnsubscribeButton(_folderPathCopyButton, OnCopyButtonClicked);
            UnsubscribeButton(_diffuseMapButton, OnDiffuseMapButtonClicked);
            UnsubscribeButton(_normalMapButton, OnNormalMapButtonClicked);
            UnsubscribeButton(_applyButton, OnApplyButtonClicked);
            _textureListView.selectionChanged -= OnTextureSelected;

            void UnsubscribeButton(Button button, Action callback)
            {
                if (button == null)
                {
                    return;
                }

                button.clicked -= callback;
            }
        }

        private IEnumerable<string> GetTexturePaths(string[] folders)
        {
            if (folders == null || folders.Length > 0 && folders[0] == null)
            {
                return new List<string>();
            }

            return AssetDatabase.FindAssets("t: Texture", folders)
                .Select(AssetDatabase.GUIDToAssetPath);
        }

        private List<Texture2D> GetTextures(List<string> paths, TextureImporterType textureType)
        {
            var textures = new List<Texture2D>();
            Texture2D texture;
            TextureImporter importer;

            foreach (var path in paths)
            {
                texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                importer = GetAssetImporter(texture);
                if (importer != null && importer.textureType == textureType)
                {
                    textures.Add(texture);
                }
            }

            return textures;
        }

        private List<TextureImporter> GetAssetImporters(List<Texture2D> textures)
        {
            var textureImporters = new List<TextureImporter>();
            TextureImporter importer;

            foreach (var texture in textures)
            {
                importer = GetAssetImporter(texture);
                if (importer != null)
                {
                    textureImporters.Add(importer);
                }
            }

            return textureImporters;
        }

        private void UpdateTextureListView(ref ListView listView, List<Texture2D> textures)
        {
            Func<VisualElement> makeItem = () => new ObjectField();
            Action<VisualElement, int> bindItem = (e, i) =>
            {
                var objectField = e as ObjectField;
                objectField.objectType = typeof(Texture2D);
                objectField.allowSceneObjects = false;

                if (i >= 0 && i < textures.Count)
                {
                    objectField.value = textures[i];
                }
            };

            listView.makeItem = makeItem;
            listView.bindItem = bindItem;
            listView.itemsSource = textures;
            listView.selectionChanged -= OnTextureSelected;
            listView.selectionChanged += OnTextureSelected;

            _textures = textures;
            _applyButton.SetEnabled(textures.Count > 0);
        }

        private void OnTextureSelected(IEnumerable<object> obj)
        {
            var texture = obj.FirstOrDefault() as Texture2D;

            if (texture)
            {
                var background = _textureIMGUI.style.backgroundImage.value;
                background.texture = texture;
                var styleBackground = _textureIMGUI.style.backgroundImage;
                styleBackground.value = background;
                _textureIMGUI.style.backgroundImage = styleBackground;

                if (_textureLabel.visible)
                {
                    _textureLabel.visible = false;
                }
            }
        }

        private void OnCopyButtonClicked()
        {
            var path = EditorUtility.OpenFolderPanel(title: "Select Folder", "", "");

            if (!string.IsNullOrEmpty(path))
            {
                path = path.Substring(path.IndexOf("Assets"));
                _path = path;
                _folderPathTextField.value = _path;
            }
        }

        private void OnDiffuseMapButtonClicked()
        {
            UpdateTextureListView(ref _textureListView, GetTextures(GetTexturePaths(new[] { _path }).ToList(),
                TextureImporterType.Default));
        }

        private void OnNormalMapButtonClicked()
        {
            UpdateTextureListView(ref _textureListView, GetTextures(GetTexturePaths(new[] { _path }).ToList(),
                TextureImporterType.NormalMap));
        }

        private void OnApplyButtonClicked()
        {
            var importers = GetAssetImporters(_textures);

            foreach (var importer in importers)
            {
                importer.mipmapEnabled = _generateMipmapsToggle.value;
                importer.streamingMipmaps = _mipStreamingToggle.value;
                importer.maxTextureSize = _resolutionMap[_maxSizeDropdownField.value];
                importer.textureCompression = _compressionMap[_compressionDropdownField.value];
                importer.crunchedCompression = _useCrunchCompressionToggle.value;

                EditorUtility.SetDirty(importer);
                importer.SaveAndReimport();
            }
        }

        private TextureImporter GetAssetImporter(Texture texture) =>
            AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture)) as TextureImporter;
    }
}
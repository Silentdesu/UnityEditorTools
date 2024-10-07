using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace EditorTools.ColliderIssueFinder
{
    public sealed class ColliderIssueFinderEditorWindow : EditorWindow
    {
        [SerializeField] private VisualTreeAsset _visualTreeAsset;

        private Label _objectsAmountLabel;
        private ListView _objectsListView;
        private Button _findInPrefabsButton;
        private Button _findInCurrentSceneButton;

        private static readonly Vector2 WINDOW_SIZE = new(350.0f, 450.0f);
        
        private const string TOOL_NAME = "Collider Issue Finder";
        private const string OBJECTS_AMOUNT_LABEL = "objects-amount-label";
        private const string OBJECTS_LIST_VIEW = "objects-list-view";
        private const string FIND_IN_PREFABS_BUTTOn = "find-in-prefabs-button";
        private const string FIND_IN_CURRENT_SCENE_BUTTON = "find-in-current-scene-button";
        private const string PROJECT_FOLDER_PATH = "Assets";
        private const string PREFAB_FILTER = "t: Prefab";
        private const string DEFAULT_LABEL_TEXT = "Objects with problem:";

        [MenuItem(Constants.TOOLS_PATH + TOOL_NAME)]
        private static void ShowEditorWindow()
        {
            var window = GetWindow<ColliderIssueFinderEditorWindow>(true, TOOL_NAME, true);
            window.maxSize = window.minSize = WINDOW_SIZE;
            window.ShowUtility();
        }

        private void OnEnable()
        {
            Init();

            void Init()
            {
                _visualTreeAsset.CloneTree(rootVisualElement);

                _objectsAmountLabel = rootVisualElement.Q<Label>(OBJECTS_AMOUNT_LABEL);
                _objectsListView = rootVisualElement.Q<ListView>(OBJECTS_LIST_VIEW);
                _findInPrefabsButton = rootVisualElement.Q<Button>(FIND_IN_PREFABS_BUTTOn);
                _findInCurrentSceneButton = rootVisualElement.Q<Button>(FIND_IN_CURRENT_SCENE_BUTTON);

                SubscribeButton(_findInPrefabsButton, OnFindInPrefabsButtonClick);
                SubscribeButton(_findInCurrentSceneButton, OnFindInCurrentSceneButtonClick);
            }

            void SubscribeButton(Button button, Action callback)
            {
                if (button != null)
                {
                    button.clicked += callback;
                }
            }
        }

        private void OnDisable()
        {
            UnsubscribeButton(_findInPrefabsButton, OnFindInPrefabsButtonClick);
            UnsubscribeButton(_findInCurrentSceneButton, OnFindInCurrentSceneButtonClick);

            void UnsubscribeButton(Button button, Action callback)
            {
                if (button != null)
                {
                    button.clicked -= callback;
                }
            }
        }

        private void OnFindInPrefabsButtonClick()
        {
            var problems = GetProblemCollidersInPrefabs();
            UpdateLabel(problems.Count.ToString());
            UpdateListView(ref _objectsListView, problems);
        }

        private void OnFindInCurrentSceneButtonClick()
        {
            var problems = GetProblemCollidersInCurrentScene();
            UpdateLabel(problems.Count.ToString());
            UpdateListView(ref _objectsListView, problems);
        }

        private List<GameObject> GetProblemCollidersInPrefabs()
        {
            var assetPaths = AssetDatabase
                .FindAssets(PREFAB_FILTER, new[] { PROJECT_FOLDER_PATH })
                .Select(AssetDatabase.GUIDToAssetPath);

            var results = new List<GameObject>();
            GameObject go = null;
            Collider[] colliders = null;

            foreach (var path in assetPaths)
            {
                go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                colliders = go.GetComponentsInChildren<Collider>();

                foreach (var collider in colliders)
                {
                    var isSizeProblem = AnyValueLessZero(collider.bounds.size);
                    var isScaleProblem = AnyValueLessZero(collider.transform.localScale);
                    var anyParentsHaveProblems = AllSubParentHasScaleIssue(collider, out var root);
                    var hasProblem = isSizeProblem || isScaleProblem || anyParentsHaveProblems;

                    if (hasProblem)
                    {
                        results.Add(go);
                        break;
                    }
                }
            }

            return results;
        }

        private List<GameObject> GetProblemCollidersInCurrentScene()
        {
            var results = new List<GameObject>();
            var rootObjects = EditorSceneManager.GetActiveScene().GetRootGameObjects();

            foreach (var rootObj in rootObjects)
            {
                foreach (var collider in rootObj.GetComponentsInChildren<Collider>())
                {
                    var isSizeProblem = AnyValueLessZero(collider.bounds.size);
                    var isScaleProblem = AnyValueLessZero(collider.transform.localScale);
                    var anyParentsHaveProblems = AllSubParentHasScaleIssue(collider, out var root);
                    var hasProblem = isSizeProblem || isScaleProblem || anyParentsHaveProblems;

                    if (hasProblem && !results.Contains(root.gameObject))
                    {
                        results.Add(root.gameObject);
                    }
                }
            }


            return results;
        }

        private void UpdateLabel(string newText)
        {
            if (_objectsAmountLabel == null)
            {
                Debug.LogError($"Couldn't find label");
                return;
            }

            _objectsAmountLabel.text = $"{DEFAULT_LABEL_TEXT} {newText}";
        }

        private void UpdateListView(ref ListView listView, List<GameObject> objects)
        {
            Func<VisualElement> makeItem = () => new ObjectField();
            Action<VisualElement, int> bindItem = (e, i) =>
            {
                var objectField = e as ObjectField;
                objectField.objectType = typeof(GameObject);
                objectField.allowSceneObjects = false;
                objectField.value = objects[i];
            };

            listView.makeItem = makeItem;
            listView.bindItem = bindItem;
            listView.itemsSource = objects;
        }

        private bool AllSubParentHasScaleIssue(Collider collider, out Transform root)
        {
            root = collider.transform.root;

            if (collider.transform.parent == null)
            {
                return false;
            }

            if (AnyValueLessZero(collider.transform.root.localScale))
            {
                return true;
            }

            var current = collider.transform;
            var hasProblem = false;
            root = current;

            while (current != null)
            {
                hasProblem = AnyValueLessZero(current.transform.localScale);

                if (hasProblem)
                {
                    break;
                }

                current = current.parent;
            }

            root = current;
            return hasProblem;
        }

        private bool AnyValueLessZero(Vector3 vec3) => vec3.x < 0.0f || vec3.y < 0.0f || vec3.z < 0.0f;
    }
}
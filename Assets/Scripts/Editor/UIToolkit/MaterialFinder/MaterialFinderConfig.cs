using UnityEditor;
using UnityEngine;

namespace EditorTools.MaterialFinder
{
    [CreateAssetMenu(menuName = Constants.CONFIG_PATH + "Material Finder")]
    public sealed class MaterialFinderConfig : ScriptableSingleton<MaterialFinderConfig>
    {
        [field: SerializeField] public string DefaultShader { get; private set; } = "Standard";
        [field: SerializeField] public string[] Shaders { get; private set; } = new[]
        {
            "Standard",
            "Universal Render Pipeline/Lit",
            "Universal Render Pipeline/Simple Lit"
        };
    }
}
using System.Collections.Generic;

namespace LoogaSoft.PrefabBrowser.Runtime
{
    [System.Serializable]
    public class BrowserCategory
    {
        public string name;
        public List<string> subCategories = new();
        public bool isExpanded = true;
    }
}

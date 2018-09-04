using System;

namespace Assets.ARCapt
{
    [Serializable]
    public class Collection
    {
        public string id { get; set; }

        public string name { get; set; }

        public int itemsCount { get; set; }
    }
}

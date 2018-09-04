using System;

namespace Assets.ARCapt
{
    [Serializable]
    public class Model
    {
        public string id { get; set; }

        public string name { get; set; }

        public string info { get; set; }

        public string thumbnail { get; set; }

        public string blobFile { get; set; }

        public string collectionId { get; set; }

        public bool isPublished { get; set; }

        public ModelMetadata metadata { get; set; }
    }
}

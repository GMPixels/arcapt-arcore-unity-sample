using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Assets.ARCapt
{
    public class CloudLoader
    {
        private string username;
        private string apiKey;

        public string StatusMsg { get; set; }

        public string ModelPath { get; set; }

        public WWW WWWLoader { get; set; }

        public Collection[] Collections { get; set; }

        public Model[] CurrentModels { get; set; }

        public CloudLoader(string username, string apiKey)
        {
            this.username = username;
            this.apiKey = apiKey;
        }

        public IEnumerator LoadCollections()
        {
            this.StatusMsg = "Loading Collections....";

            this.WWWLoader = new WWW("http://www.arcapt.com/api/v1/collections?api_key=" + this.apiKey);
            yield return WWWLoader;

            if (!this.WWWLoader.isDone)
            {
                yield return this.WWWLoader;
            }

            this.Collections = JsonConvert.DeserializeObject<Collection[]>(this.WWWLoader.text);

            var options = new List<string>() { "Collections" };
            for (int i = 0; i < this.Collections.Length; i++)
            {
                options.Add(this.Collections[i].name);
            }
            var dropdownCollections = GameObject.Find("DropdownCollections").GetComponent<UnityEngine.UI.Dropdown>();
            dropdownCollections.AddOptions(options);
            this.StatusMsg = this.Collections.Length + " collections loaded";

        }

        public IEnumerator LoadModels(string collectionId)
        {
            this.StatusMsg = "Loading Models....";

            this.WWWLoader = new WWW(string.Format(@"http://www.arcapt.com/api/v1/collections/{0}/items?api_key={1}", collectionId, this.apiKey));
            yield return WWWLoader;

            if (!this.WWWLoader.isDone)
            {
                yield return this.WWWLoader;
            }

            this.CurrentModels = JsonConvert.DeserializeObject<Model[]>(this.WWWLoader.text);

            var options = new List<UnityEngine.UI.Dropdown.OptionData>() { new UnityEngine.UI.Dropdown.OptionData { text = "Models" } };
            for (int i = 0; i < this.CurrentModels.Length; i++)
            {
                var model = this.CurrentModels[i];
                var option = new UnityEngine.UI.Dropdown.OptionData { text = model.name };
                options.Add(option);
                if (!string.IsNullOrEmpty(model.thumbnail))
                {
                    // DownloadImage(model.thumbnail, option);
                }
            }
            var dropdownModels = GameObject.Find("DropdownModels").GetComponent<UnityEngine.UI.Dropdown>();
            dropdownModels.ClearOptions();
            dropdownModels.AddOptions(options);

            dropdownModels.value = 0;
            dropdownModels.Select();
            dropdownModels.RefreshShownValue();

            this.StatusMsg = "Select a 3D model to load.";
        }

        public IEnumerator LoadModel(string modelId)
        {
            if (string.IsNullOrEmpty(modelId))
            {
                throw new InvalidOperationException("Missing model id");
            }

            var output = Path.Combine(Application.persistentDataPath, modelId);
            if (Directory.Exists(output))
            {
                this.StatusMsg = "Reading 3D model from cache...";
            }
            else
            {
                Directory.CreateDirectory(output);
                this.StatusMsg = "Downloading 3D model...";

                this.WWWLoader = new WWW(string.Format("https://send3d.blob.core.windows.net/{0}/{1}/{2}", this.username, modelId, modelId));
                yield return WWWLoader;

                if (!this.WWWLoader.isDone)
                {
                    yield return this.WWWLoader;
                }
                if (!Directory.Exists(output))
                {
                    Directory.CreateDirectory(output);
                }

                this.StatusMsg = "3D Model downloaded. Saving to phone....";
                ExtractZipFile(this.WWWLoader.bytes, output);
            }

            try
            {
                var gltf = Directory.GetFiles(output, "*.gltf");
                if (gltf != null && gltf.Length > 0)
                {
                    this.ModelPath = gltf[0];
                    this.StatusMsg = "3D Model ready. Tap on surface to load it.";
                }
            }
            catch (Exception)
            {
                this.StatusMsg = "Error loading 3D model.";
            }
        }

        private void ExtractZipFile(byte[] data, string outFolder)
        {
            ZipFile zf = null;
            using (var mstrm = new MemoryStream(data))
            {
                zf = new ZipFile(mstrm);

                foreach (ZipEntry zipEntry in zf)
                {
                    if (zipEntry.IsDirectory)
                    {
                        string dirPath = Path.Combine(outFolder, zipEntry.Name);

                        if (!Directory.Exists(dirPath))
                        {
                            Directory.CreateDirectory(dirPath);
                        }
                    }

                    if (zipEntry.IsFile)
                    {
                        var entryFileName = zipEntry.Name;
                        var buffer = new byte[4096];     // 4K is optimum
                        var zipStream = zf.GetInputStream(zipEntry);

                        var fullZipToPath = Path.Combine(outFolder, entryFileName);
                        var directory = Path.GetDirectoryName(fullZipToPath);
                        if (!Directory.Exists(directory))
                        {
                            Directory.CreateDirectory(directory);
                        }
                        using (FileStream streamWriter = File.Create(fullZipToPath))
                        {
                            StreamUtils.Copy(zipStream, streamWriter, buffer);
                        }
                    }
                }
            }

            if (zf != null)
            {
                zf.IsStreamOwner = true;
                zf.Close();
            }
        }

        private IEnumerator DownloadImage(string url, UnityEngine.UI.Dropdown.OptionData option)
        {
            WWW www = new WWW(url);
            yield return www;
            option.image = Sprite.Create(www.texture, new Rect(0, 0, 50, 50), new Vector2(0, 0));
        }
    }
}

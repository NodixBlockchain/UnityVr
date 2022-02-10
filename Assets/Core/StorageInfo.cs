namespace DataTanker
{
    using System.IO;
    using System.Runtime.Serialization;
    using System.Runtime.Serialization.Json;
    using System.Text;

    using System.Xml.Serialization;

    /// <summary>
    /// Container for general information about storage.
    /// </summary>
    [System.Serializable]
    public class StorageInfo
    {
        public string StorageClrTypeName { get; set; }

        public string KeyClrTypeName { get; set; }

        public string ValueClrTypeName { get; set; }

        public int MaxKeyLength { get; set; }

        public override string ToString()
        {
            var ms = new MemoryStream();


            XmlSerializer serializer = new XmlSerializer(typeof(StorageInfo));
            serializer.Serialize(ms, this);
            return Encoding.UTF8.GetString(ms.ToArray());


            //return JsonUtility.ToJson(this);

            /*
            var serializer = new DataContractJsonSerializer(typeof(StorageInfo));
            using (var ms = new MemoryStream())
            {
                serializer.WriteObject(ms, this);
                ms.Flush();
                return Encoding.UTF8.GetString(ms.ToArray());
            }
            */
        }

        public static StorageInfo FromString(string str)
        {
            //return JsonUtility.FromJson<StorageInfo>(str);

            XmlSerializer serializer = new XmlSerializer(typeof(StorageInfo));
            MemoryStream reader = new MemoryStream(Encoding.UTF8.GetBytes(str));
            return (StorageInfo)serializer.Deserialize(reader);

            /*
            var serializer = new DataContractJsonSerializer(typeof(StorageInfo));
            using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(str)))
            {
                return serializer.ReadObject(ms) as StorageInfo;
            }
            */
        }
    }
}
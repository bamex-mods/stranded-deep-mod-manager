using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace StrandedDeepModManager
{
    internal static class JsonUtil
    {
        private static readonly JavaScriptSerializer Serializer = CreateSerializer();

        private static JavaScriptSerializer CreateSerializer()
        {
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = int.MaxValue;
            serializer.RecursionLimit = 200;
            return serializer;
        }

        public static T ReadFile<T>(string path)
        {
            string text = File.ReadAllText(path, Encoding.UTF8);
            return Serializer.Deserialize<T>(text);
        }

        public static void WriteFile<T>(string path, T value)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string json = Serializer.Serialize(value);
            File.WriteAllText(path, json, new UTF8Encoding(false));
        }

        public static T Deserialize<T>(string text)
        {
            return Serializer.Deserialize<T>(text);
        }
    }
}

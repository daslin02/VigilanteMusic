using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

namespace VigilanteMusic.Helpers
{
    /// <summary>
    /// Simple JSON (de)serialization of objects. No dependencies required.
    /// </summary>
    public static class JsonHelper
    {
        /// <summary>Serialize an object to a JSON string (with indentation).</summary>
        public static string Serialize<T>(T obj, bool indented = true)
        {
            if (obj == null) return null;

            var settings = new DataContractJsonSerializerSettings
            {
                UseSimpleDictionaryFormat = true,
                DateTimeFormat = new System.Runtime.Serialization.DateTimeFormat("yyyy-MM-ddTHH:mm:ss")
            };
            var serializer = new DataContractJsonSerializer(typeof(T), settings);

            using (var ms = new MemoryStream())
            {
                using (var writer = JsonReaderWriterFactory.CreateJsonWriter(
                           ms, Encoding.UTF8, true, indented, "  "))
                {
                    serializer.WriteObject(writer, obj);
                    writer.Flush();
                }
                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }

        /// <summary>Deserialize a JSON string. Returns default(T) in case of an error.</summary>
        public static T Deserialize<T>(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return default(T);

            try
            {
                var settings = new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true };
                var serializer = new DataContractJsonSerializer(typeof(T), settings);

                using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                {
                    return (T)serializer.ReadObject(ms);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[JsonHelper] Deserialize error: {ex.Message}");
                return default(T);
            }
        }

        /// <summary>Save the object to a file (atomically: first to a .tmp file, then replace).</summary>
        public static bool SaveToFile<T>(string filePath, T obj)
        {
            try
            {
                string dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                string json = Serialize(obj);
                string tmp = filePath + ".tmp";

                File.WriteAllText(tmp, json, new UTF8Encoding(false));

                if (File.Exists(filePath))
                {
                    File.Copy(tmp, filePath, true);
                    File.Delete(tmp);
                }
                else
                {
                    File.Move(tmp, filePath);
                }
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[JsonHelper] Save error: {ex.Message}");
                return false;
            }
        }

        /// <summary>Load an object from a file. If the file is missing or corrupt, it returns default(T).</summary>
        public static T LoadFromFile<T>(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return default(T);
                return Deserialize<T>(File.ReadAllText(filePath, Encoding.UTF8));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[JsonHelper] Load error: {ex.Message}");
                return default(T);
            }
        }

        /// <summary>Load or create a default object and save it immediately.</summary>
        public static T LoadOrCreate<T>(string filePath, Func<T> factory)
        {
            T obj = LoadFromFile<T>(filePath);
            if (obj == null)
            {
                obj = factory();
                SaveToFile(filePath, obj);
            }
            return obj;
        }
    }
}
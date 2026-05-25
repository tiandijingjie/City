using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using LitJson;

namespace WarField
{
    public static class JsonUtils
    {
        //读取json文件
        public static JsonData LoadJsonData(string folderPath, string fileName)
        {
            if (string.IsNullOrEmpty(folderPath) || string.IsNullOrEmpty(fileName))
            {
                GameLogger.LogError("Path or file name cannot be null or empty");
                return null;
            }

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            var fullPath = Path.Combine(folderPath, fileName);
            return LoadJsonData(fullPath);
        }

        public static JsonData LoadJsonData(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                GameLogger.LogError("Path or file name cannot be null or empty");
                return null;
            }

            if (!File.Exists(filePath))
            {
                GameLogger.LogError($"File not found {filePath}");
                return null;
            }

            try
            {
                var json = File.ReadAllText(filePath);
                return JsonMapper.ToObject(json);
            }
            catch (Exception ex)
            {
                GameLogger.LogError($"Deserialization failed: {ex.Message}");
                return null;
            }
        }

        public static T LoadJsonData<T>(string folderPath, string fileName)
        {
            if (string.IsNullOrEmpty(folderPath) || string.IsNullOrEmpty(fileName))
            {
                GameLogger.LogError("Path or file name cannot be null or empty");
                return default(T);
            }

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            var fullPath = Path.Combine(folderPath, fileName);
            return LoadJsonData<T>(fullPath);
        }

        public static T LoadJsonData<T>(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                GameLogger.LogError("Path or file name cannot be null or empty");
                return default(T);
            }

            if (!File.Exists(filePath))
            {
                GameLogger.LogError($"File not found {filePath}");
                return default(T);
            }

            try
            {
                var json = File.ReadAllText(filePath);
                return JsonMapper.ToObject<T>(json);
            }
            catch (Exception ex)
            {
                GameLogger.LogError($"Deserialization failed: {ex.Message}");
                return default(T);
            }
        }

        //将jsonData格式化之后保存到文件,覆盖整个文件
        public static bool SaveJsonData(string folderPath, string fileName, object jsonData)
        {
            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            var filePath = Path.Combine(folderPath, fileName);
            return SaveJsonData(filePath, jsonData);
        }

        public static bool SaveJsonData(string filePath, object jsonData)
        {
            if (jsonData == null)
            {
                GameLogger.LogError("jsonData is null, cannot save");
                return false;
            }

            try
            {
                var writer = new JsonWriter
                {
                    PrettyPrint = true,
                    IndentValue = 4
                };
                JsonMapper.ToJson(jsonData, writer);

                File.WriteAllText(filePath, writer.ToString());

                return true;
            }
            catch (System.Exception ex)
            {
                GameLogger.LogError($"Write failed: {ex.Message}");
                return false;
            }
        }

        //读取rootData中key字段的值
        public static T ReadJsonField<T>(JsonData rootData, string key)
        {
            if (rootData == null || !rootData.IsObject || !((IDictionary)rootData).Contains(key))
            {
                GameLogger.LogError($"Key '{key}' not found.");
                return default;
            }

            try
            {
                var fieldData = rootData[key];

                if (typeof(T) == typeof(string))
                {
                    var raw = fieldData.ToString();
                    if (raw.Length >= 2 && raw[0] == '\"' && raw[raw.Length - 1] == '\"')
                    {
                        raw = raw.Substring(1, raw.Length - 2);
                    }

                    return (T)(object)raw;
                }

                var jsonStr = JsonMapper.ToJson(fieldData);
                return JsonMapper.ToObject<T>(jsonStr);
            }
            catch (Exception ex)
            {
                GameLogger.LogError($"Failed to read key '{key}': {ex.Message}");
                return default;
            }
        }

        //将json数据rootData中key对应的字段的字段改成obj
        public static void WriteJsonField<T>(JsonData rootData, string key, T obj)
        {
            if (rootData == null)
            {
                GameLogger.LogError($"rootData is null.");
                return;
            }

            try
            {
                var jsonStr = JsonMapper.ToJson(obj);
                var fieldData = JsonMapper.ToObject(jsonStr);
                rootData[key] = fieldData;
            }
            catch (Exception ex)
            {
                GameLogger.LogError($"Failed to write key '{key}': {ex.Message}");
            }
        }

        //将一个类obj转成格式化的json字符串
        public static string ConvertFormatJson(object obj)
        {
            JsonWriter writer = new JsonWriter
            {
                PrettyPrint = true,
                IndentValue = 4
            };
            JsonMapper.ToJson(obj, writer);
            return writer.ToString();
        }
    }
}

using System;
using System.IO;
using System.Text;
using UnityEngine;
using Devian.Domain.Common;

namespace Devian
{
    public static class LocalSaveFileStore
    {
        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        public static CoreResult<bool> WriteAtomic(string rootPath, string filename, LocalSavePayload payload)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(rootPath))
                {
                    return CoreResult<bool>.Failure(ErrorClientType.LOCALSAVE_PATH_EMPTY, "Root path is empty.");
                }

                if (string.IsNullOrWhiteSpace(filename))
                {
                    return CoreResult<bool>.Failure(ErrorClientType.LOCALSAVE_FILENAME_EMPTY, "Filename is empty.");
                }

                var path = Path.Combine(rootPath, filename);
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                var tmpPath = path + ".tmp." + Guid.NewGuid().ToString("N");

                var json = JsonUtility.ToJson(payload);
                File.WriteAllText(tmpPath, json, Utf8NoBom);

                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                File.Move(tmpPath, path);

                return CoreResult<bool>.Success(true);
            }
            catch (Exception ex)
            {
                return CoreResult<bool>.Failure(ErrorClientType.LOCALSAVE_WRITE, ex.Message);
            }
        }

        public static CoreResult<LocalSavePayload> Read(string rootPath, string filename)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(rootPath))
                {
                    return CoreResult<LocalSavePayload>.Failure(ErrorClientType.LOCALSAVE_PATH_EMPTY, "Root path is empty.");
                }

                if (string.IsNullOrWhiteSpace(filename))
                {
                    return CoreResult<LocalSavePayload>.Failure(ErrorClientType.LOCALSAVE_FILENAME_EMPTY, "Filename is empty.");
                }

                var path = Path.Combine(rootPath, filename);
                if (!File.Exists(path))
                {
                    return CoreResult<LocalSavePayload>.Success(null);
                }

                var json = File.ReadAllText(path, Utf8NoBom);
                var payload = JsonUtility.FromJson<LocalSavePayload>(json);
                return CoreResult<LocalSavePayload>.Success(payload);
            }
            catch (Exception ex)
            {
                return CoreResult<LocalSavePayload>.Failure(ErrorClientType.LOCALSAVE_READ, ex.Message);
            }
        }
    }
}

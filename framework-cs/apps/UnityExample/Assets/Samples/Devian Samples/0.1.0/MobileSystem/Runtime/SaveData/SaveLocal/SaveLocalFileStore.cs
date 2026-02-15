using System;
using System.IO;
using System.Text;
using UnityEngine;
using Devian.Domain.Common;

namespace Devian
{
    public static class SaveLocalFileStore
    {
        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        public static CommonResult<bool> WriteAtomic(string rootPath, string filename, SaveLocalPayload payload)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(rootPath))
                {
                    return CommonResult<bool>.Failure(CommonErrorType.LOCALSAVE_PATH_EMPTY, "Root path is empty.");
                }

                if (string.IsNullOrWhiteSpace(filename))
                {
                    return CommonResult<bool>.Failure(CommonErrorType.LOCALSAVE_FILENAME_EMPTY, "Filename is empty.");
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

                return CommonResult<bool>.Success(true);
            }
            catch (Exception ex)
            {
                return CommonResult<bool>.Failure(CommonErrorType.LOCALSAVE_WRITE, ex.Message);
            }
        }

        public static CommonResult<SaveLocalPayload> Read(string rootPath, string filename)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(rootPath))
                {
                    return CommonResult<SaveLocalPayload>.Failure(CommonErrorType.LOCALSAVE_PATH_EMPTY, "Root path is empty.");
                }

                if (string.IsNullOrWhiteSpace(filename))
                {
                    return CommonResult<SaveLocalPayload>.Failure(CommonErrorType.LOCALSAVE_FILENAME_EMPTY, "Filename is empty.");
                }

                var path = Path.Combine(rootPath, filename);
                if (!File.Exists(path))
                {
                    return CommonResult<SaveLocalPayload>.Success(null);
                }

                var json = File.ReadAllText(path, Utf8NoBom);
                var payload = JsonUtility.FromJson<SaveLocalPayload>(json);
                return CommonResult<SaveLocalPayload>.Success(payload);
            }
            catch (Exception ex)
            {
                return CommonResult<SaveLocalPayload>.Failure(CommonErrorType.LOCALSAVE_READ, ex.Message);
            }
        }
    }
}

using System;
using System.IO;
using System.Text;
using UnityEngine;
using Devian.Domain.Common;
using Devian.Domain.Game;

namespace Devian
{
    public static class SaveLocalFileStore
    {
        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        public static GameResult<bool> WriteAtomic(string rootPath, string filename, SaveLocalPayload payload)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(rootPath))
                {
                    return GameResult<bool>.Failure(GAME_ERROR_TYPE.LOCALSAVE_PATH_EMPTY, "Root path is empty.");
                }

                if (string.IsNullOrWhiteSpace(filename))
                {
                    return GameResult<bool>.Failure(GAME_ERROR_TYPE.LOCALSAVE_FILENAME_EMPTY, "Filename is empty.");
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

                return GameResult<bool>.Success(true);
            }
            catch (Exception ex)
            {
                return GameResult<bool>.Failure(GAME_ERROR_TYPE.LOCALSAVE_WRITE, ex.Message);
            }
        }

        public static GameResult<SaveLocalPayload> Read(string rootPath, string filename)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(rootPath))
                {
                    return GameResult<SaveLocalPayload>.Failure(GAME_ERROR_TYPE.LOCALSAVE_PATH_EMPTY, "Root path is empty.");
                }

                if (string.IsNullOrWhiteSpace(filename))
                {
                    return GameResult<SaveLocalPayload>.Failure(GAME_ERROR_TYPE.LOCALSAVE_FILENAME_EMPTY, "Filename is empty.");
                }

                var path = Path.Combine(rootPath, filename);
                if (!File.Exists(path))
                {
                    return GameResult<SaveLocalPayload>.Success(null);
                }

                var json = File.ReadAllText(path, Utf8NoBom);
                var payload = JsonUtility.FromJson<SaveLocalPayload>(json);
                return GameResult<SaveLocalPayload>.Success(payload);
            }
            catch (Exception ex)
            {
                return GameResult<SaveLocalPayload>.Failure(GAME_ERROR_TYPE.LOCALSAVE_READ, ex.Message);
            }
        }
    }
}

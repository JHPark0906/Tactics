using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace HS.Framework.Persistence
{
    /// <summary>
    /// Application.persistentDataPath 하위에 키별 JSON 파일로 기록하는 저장소 백엔드이다.
    /// 임시 파일에 먼저 쓴 뒤 교체하는 원자적 쓰기로, 기록 중 중단되어도 기존 파일이 손상되지 않는다.
    /// </summary>
    public sealed class FileSaveDataStorage : ISaveDataStorage
    {
        /// <summary>
        /// 루트 디렉터리를 지정하지 않았을 때 persistentDataPath 아래에 사용하는 폴더 이름이다.
        /// </summary>
        private const string DefaultDirectoryName = "SaveData";

        /// <summary>
        /// 저장 파일의 확장자이다.
        /// </summary>
        private const string FileExtension = ".json";

        /// <summary>
        /// 원자적 쓰기에 사용하는 임시 파일 접미사이다.
        /// </summary>
        private const string TempFileSuffix = ".tmp";

        // 이전 파일명과 새 인코딩이 서로 충돌하지 않도록 저장 형식의 디렉터리도 분리한다.
        private const string FormatDirectoryName = "v2";

        /// <summary>
        /// 저장 파일을 보관하는 루트 디렉터리 경로이다.
        /// </summary>
        private readonly string _rootDirectory;

        /// <summary>
        /// 파일 저장소 백엔드를 생성한다.
        /// </summary>
        /// <param name="rootDirectory">저장 파일을 보관할 디렉터리이며, null이면 Application.persistentDataPath 하위 SaveData를 사용한다.</param>
        public FileSaveDataStorage(string rootDirectory = null)
        {
            _rootDirectory = string.IsNullOrWhiteSpace(rootDirectory)
                ? Path.Combine(Application.persistentDataPath, DefaultDirectoryName)
                : rootDirectory;
        }

        /// <summary>
        /// 저장 파일을 보관하는 루트 디렉터리 경로를 가져온다.
        /// </summary>
        public string RootDirectory => _rootDirectory;

        /// <inheritdoc />
        public bool Exists(string key)
        {
            ValidateKey(key);
            return File.Exists(GetFilePath(key)) || FindLegacyFilePath(key) != null;
        }

        /// <inheritdoc />
        public bool TryRead(string key, out string value)
        {
            ValidateKey(key);
            var filePath = GetFilePath(key);
            if (!File.Exists(filePath))
            {
                filePath = FindLegacyFilePath(key);
                if (filePath == null)
                {
                    value = null;
                    return false;
                }
            }

            value = File.ReadAllText(filePath);
            return true;
        }

        /// <inheritdoc />
        public void Write(string key, string value)
        {
            ValidateKey(key);
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            var filePath = GetFilePath(key);
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            var tempFilePath = filePath + TempFileSuffix;
            File.WriteAllText(tempFilePath, value);

            if (File.Exists(filePath))
            {
                File.Replace(tempFilePath, filePath, null);
            }
            else
            {
                File.Move(tempFilePath, filePath);
            }
        }

        /// <inheritdoc />
        public void Delete(string key)
        {
            ValidateKey(key);
            var filePath = GetFilePath(key);
            var tempFilePath = filePath + TempFileSuffix;
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }

            // 새 저장본을 지운 뒤 이전 저장본이 되살아나지 않도록 같은 키의 이전 파일도 지운다.
            var legacyFilePath = FindLegacyFilePath(key);
            if (legacyFilePath != null)
            {
                File.Delete(legacyFilePath);
            }

            var legacyTempFilePath = FindLegacyFilePath(key, TempFileSuffix);
            if (legacyTempFilePath != null)
            {
                File.Delete(legacyTempFilePath);
            }
        }

        /// <summary>
        /// 저장소 키를 파일 이름으로 이스케이프해 전체 경로로 변환한다.
        /// 대문자도 인코딩하므로 대소문자를 구별하지 않는 파일 시스템에서도 서로 다른 키가 보존된다.
        /// 접두사로 Windows 예약 파일명을 피하고, 경로 구분자는 인코딩한다.
        /// </summary>
        /// <param name="key">변환할 저장소 키이다.</param>
        /// <returns>키에 대응하는 저장 파일의 전체 경로를 반환한다.</returns>
        private string GetFilePath(string key)
        {
            return Path.Combine(_rootDirectory, FormatDirectoryName, "key-" + EscapeKeyToFileName(key) + FileExtension);
        }

        /// <summary>
        /// 이전 형식의 저장본은 실제 파일명의 대소문자가 일치할 때만 읽는다. Windows의 File.Exists만
        /// 사용하면 Player의 파일을 player가 가져가므로 실제 디렉터리 항목으로 소유 키를 구분한다.
        /// 새 형식으로 저장한 뒤에도 이전 파일은 보존하고 새 파일을 우선 읽는다.
        /// </summary>
        private string FindLegacyFilePath(string key, string suffix = "")
        {
            var fileName = EscapeKeyToFileName(key, preserveUppercase: true) + FileExtension + suffix;
            if (!File.Exists(Path.Combine(_rootDirectory, fileName)))
            {
                return null;
            }

            foreach (var candidate in Directory.EnumerateFiles(_rootDirectory, fileName))
            {
                if (string.Equals(Path.GetFileName(candidate), fileName, StringComparison.Ordinal))
                {
                    return candidate;
                }
            }

            return null;
        }

        /// <summary>
        /// 저장소 키를 모든 플랫폼에서 유효한 파일 이름 조각으로 이스케이프한다.
        /// </summary>
        /// <param name="key">이스케이프할 저장소 키이다.</param>
        /// <returns>파일 이름에 쓸 수 있는 문자만 남긴 문자열을 반환한다.</returns>
        private static string EscapeKeyToFileName(string key, bool preserveUppercase = false)
        {
            var builder = new StringBuilder(key.Length);
            foreach (var character in key)
            {
                if (character is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-' or '_' or '.' ||
                    preserveUppercase && character is >= 'A' and <= 'Z')
                {
                    builder.Append(character);
                }
                else
                {
                    builder.Append('%');
                    builder.Append(((int)character).ToString("X4"));
                }
            }

            return builder.ToString();
        }

        /// <summary>
        /// 저장소 키가 비어 있지 않은지 검증한다.
        /// </summary>
        /// <param name="key">검증할 저장소 키이다.</param>
        private static void ValidateKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("저장소 키는 비어 있을 수 없습니다.", nameof(key));
            }
        }
    }
}

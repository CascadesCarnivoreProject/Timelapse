using Microsoft.Win32.SafeHandles;
using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using IBindCtx = System.Runtime.InteropServices.ComTypes.IBindCtx;

namespace Carnassial.Interop
{
    internal class NativeMethods
    {
        private const int FILE_ATTRIBUTE_DIRECTORY = 0x10;
        private const int FILE_ATTRIBUTE_NORMAL = 0x80;

        public static SafeFileHandle CreateFileUnbuffered(string path, bool overlapped)
        {
            FileAttributesNative fileAttributes = FileAttributesNative.NoBuffering;
            if (overlapped)
            {
                fileAttributes |= FileAttributesNative.Overlapped;
            }
            SafeFileHandle fileHandle = NativeMethods.CreateFile(path, FileAccess.Read, FileShare.Read, IntPtr.Zero, FileMode.Open, fileAttributes, IntPtr.Zero);
            if (fileHandle.IsInvalid)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            return fileHandle;
        }

        [DllImport(Constant.Assembly.Kernel32, BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "CreateFileW", SetLastError = true)]
        private static extern SafeFileHandle CreateFile(string lpFileName,
                                                        FileAccess dwDesiredAccess,
                                                        FileShare dwShareMode,
                                                        IntPtr lpSecurityAttributes,
                                                        FileMode dwCreationDisposition,
                                                        FileAttributesNative dwFlagsAndAttributes,
                                                        IntPtr hTemplateFile);

        [SupportedOSPlatform(Constant.Platform.Windows)]
        public static ComReleaser<IShellItem> CreateShellItem(string path)
        {
            Guid shellItemGuid = new(Constant.ComGuid.IShellItem);
            return new ComReleaser<IShellItem>((IShellItem)NativeMethods.SHCreateItemFromParsingName(path, null, ref shellItemGuid));
        }

        [DllImport(Constant.Assembly.Kernel32, CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool GetDiskFreeSpace(string lpRootPathName, out uint lpSectorsPerCluster, out uint lpBytesPerSector, out uint lpNumberOfFreeClusters, out uint lpTotalNumberOfClusters);

        public static long GetFileSizeEx(SafeFileHandle file)
        {
            bool success = NativeMethods.GetFileSizeEx(file, out long fileSize);
            if (success == false)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            return fileSize;
        }

        [DllImport(Constant.Assembly.Kernel32)]
        private static extern bool GetFileSizeEx(SafeFileHandle hFile, out long lpFileSize);

        public static CultureInfo GetKeyboardCulture()
        {
            long keyboardLayout = NativeMethods.GetKeyboardLayout(0).ToInt64();
            int languageID = (int)(keyboardLayout & 0x000000000000ffff);
            return new CultureInfo(languageID);
        }

        [DllImport(Constant.Assembly.User32)]
        private static extern IntPtr GetKeyboardLayout(uint idThread);

        private static string GetRelativePathFromDirectory(string? fromDirectoryPath, string? toPath, int toType)
        {
            if (fromDirectoryPath == null)
            {
                throw new ArgumentNullException(nameof(fromDirectoryPath));
            }
            if (toPath == null)
            {
                throw new ArgumentNullException(nameof(toPath));
            }

            StringBuilder relativePathBuilder = new(260); // MAX_PATH
            if (NativeMethods.PathRelativePathTo(relativePathBuilder,
                                                 fromDirectoryPath,
                                                 NativeMethods.FILE_ATTRIBUTE_DIRECTORY,
                                                 toPath,
                                                 toType) == 0)
            {
                throw new ArgumentException(App.FindResource<string>(Constant.ResourceKey.NativeMethodsPathPrefixRequired));
            }

            string relativePath = relativePathBuilder.ToString();
            if (relativePath.StartsWith(".\\", StringComparison.Ordinal))
            {
                relativePath = relativePath[2..];
            }
            return relativePath;
        }

        public static string GetRelativePathFromDirectoryToDirectory(string? fromDirectoryPath, string ?toDirectoryPath)
        {
            return NativeMethods.GetRelativePathFromDirectory(fromDirectoryPath, toDirectoryPath, NativeMethods.FILE_ATTRIBUTE_DIRECTORY);
        }

        public static string GetRelativePathFromDirectoryToFile(string fromDirectoryPath, FileInfo toFile)
        {
            return NativeMethods.GetRelativePathFromDirectory(fromDirectoryPath, toFile.FullName, NativeMethods.FILE_ATTRIBUTE_NORMAL);
        }

        public static int GetSectorSizeInBytes(string driveLetter)
        {
            bool success = NativeMethods.GetDiskFreeSpace(driveLetter, out uint sectorsPerCluster, out uint bytesPerSector, out uint freeClusters, out uint clusters);
            if (success == false)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            return (int)bytesPerSector;
        }

        [DllImport(Constant.Assembly.Kernel32)]
        public static extern SafeAccessTokenHandle GetCurrentThread();

        [DllImport(Constant.Assembly.Kernel32)]
        public static extern UInt64 GetTickCount64();

        public static void MoveToRecycleBin(string filePath)
        {
            if (String.IsNullOrEmpty(filePath))
            {
                throw new ArgumentOutOfRangeException(nameof(filePath), App.FindResource<string>(Constant.ResourceKey.NativeMethodsPathNullOrEmpty));
            }

            // per docs, SHFileOperation() won't move files to recycle bin unless presented a full path
            filePath = Path.GetFullPath(filePath);

            SHFILEOPSTRUCT moveToBin = new()
            {
                hwnd = IntPtr.Zero,
                wFunc = SHFileOpFunc.FO_DELETE,
                pFrom = filePath + "\\0\\0",
                pTo = null,
                fFlags = FILEOP_FLAGS.FOF_ALLOWUNDO | FILEOP_FLAGS.FOF_NOCONFIRMATION | FILEOP_FLAGS.FOF_NOERRORUI | FILEOP_FLAGS.FOF_SILENT,
                fAnyOperationsAborted = false,
                hNameMappings = IntPtr.Zero,
                lpszProgressTitle = null
            };
            int result = NativeMethods.SHFileOperation(ref moveToBin);
            if (result != 0)
            {
                throw new Win32Exception(result);
            }
            if (moveToBin.fAnyOperationsAborted)
            {
                throw new Win32Exception(String.Format(CultureInfo.CurrentCulture, "Move of '{0}' to recycle bin was aborted.", filePath));
            }
        }

        [DllImport(Constant.Assembly.Shlwapi, CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int PathRelativePathTo(StringBuilder pszPath, string pszFrom, int dwAttrFrom, string pszTo, int dwAttrTo);

        [DllImport(Constant.Assembly.Kernel32, SetLastError = true)]
        public static extern unsafe int ReadFile(SafeFileHandle file,
                                                 byte* buffer,
                                                 int bytesToRead,
                                                 ref int bytesRead,
                                                 NativeOverlapped* overlapped);

        public static UInt64 SetThreadAffinityMask(SafeHandle unmanagedThread, UInt64 mask)
        {
            UIntPtr previousAffinity = NativeMethods.SetThreadAffinityMask(unmanagedThread, new UIntPtr(mask));
            if (previousAffinity == UIntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            return previousAffinity.ToUInt64();
        }

        [DllImport(Constant.Assembly.Kernel32, SetLastError = true)]
        private static extern UIntPtr SetThreadAffinityMask(SafeHandle hThread, UIntPtr dwThreadAffinityMask);

        [DllImport(Constant.Assembly.Shell32, SetLastError = true, PreserveSig = false)]
        [return: MarshalAs(UnmanagedType.Interface)]
        private static extern object SHCreateItemFromParsingName([MarshalAs(UnmanagedType.LPWStr)] string pszPath, IBindCtx? pbc, ref Guid riid);

        [DllImport(Constant.Assembly.Shell32, CharSet = CharSet.Unicode)]
        private static extern int SHFileOperation([In] ref SHFILEOPSTRUCT lpFileOp);

        [Flags]
        private enum FileAttributesNative : uint
        {
            Readonly = 0x00000001,
            Hidden = 0x00000002,
            System = 0x00000004,
            Directory = 0x00000010,
            Archive = 0x00000020,
            Device = 0x00000040,
            Normal = 0x00000080,
            Temporary = 0x00000100,
            SparseFile = 0x00000200,
            ReparsePoint = 0x00000400,
            Compressed = 0x00000800,
            Offline = 0x00001000,
            NotContentIndexed = 0x00002000,
            Encrypted = 0x00004000,
            FirstPipeInstance = 0x00080000,
            OpenNoRecall = 0x00100000,
            OpenReparsePoint = 0x00200000,
            SequentialScan = 0x08000000,
            PosixSemantics = 0x01000000,
            BackupSemantics = 0x02000000,
            DeleteOnClose = 0x04000000,
            RandomAccess = 0x10000000,
            NoBuffering = 0x20000000,
            Overlapped = 0x40000000,
            WriteThrough = 0x80000000
        }

        [Flags]
        private enum FILEOP_FLAGS : ushort
        {
            FOF_MULTIDESTFILES = 0x1,
            FOF_CONFIRMMOUSE = 0x2,

            /// <summary>
            /// Don't create progress/report
            /// </summary>
            FOF_SILENT = 0x4,
            FOF_RENAMEONCOLLISION = 0x8,

            /// <summary>
            /// Don't prompt the user.
            /// </summary>
            FOF_NOCONFIRMATION = 0x10,

            /// <summary>
            /// Fill in SHFILEOPSTRUCT.hNameMappings.
            /// Must be freed using SHFreeNameMappings
            /// </summary>
            FOF_WANTMAPPINGHANDLE = 0x20,
            FOF_ALLOWUNDO = 0x40,

            /// <summary>
            /// On *.*, do only files
            /// </summary>
            FOF_FILESONLY = 0x80,

            /// <summary>
            /// Don't show names of files
            /// </summary>
            FOF_SIMPLEPROGRESS = 0x100,

            /// <summary>
            /// Don't confirm directory creation
            /// </summary>
            FOF_NOCONFIRMMKDIR = 0x200,

            /// <summary>
            /// Don't put up error UI
            /// </summary>
            FOF_NOERRORUI = 0x400,

            /// <summary>
            /// Don't copy NT file Security Attributes
            /// </summary>
            FOF_NOCOPYSECURITYATTRIBS = 0x800,

            /// <summary>
            /// Don't recurse into directories.
            /// </summary>
            FOF_NORECURSION = 0x1000,

            /// <summary>
            /// Don't operate on connected elements.
            /// </summary>
            FOF_NO_CONNECTED_ELEMENTS = 0x2000,

            /// <summary>
            /// During delete operation, 
            /// warn if nuking instead of recycling (partially overrides FOF_NOCONFIRMATION)
            /// </summary>
            FOF_WANTNUKEWARNING = 0x4000,

            /// <summary>
            /// Treat reparse points as objects, not containers
            /// </summary>
            FOF_NORECURSEREPARSE = 0x8000
        }

        private enum SHFileOpFunc : uint
        {
            FO_MOVE = 0x1,
            FO_COPY = 0x2,
            FO_DELETE = 0x3,
            FO_RENAME = 0x4
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SHFILEOPSTRUCT
        {
            public IntPtr hwnd;
            public SHFileOpFunc wFunc;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string pFrom;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string? pTo;
            public FILEOP_FLAGS fFlags;
            [MarshalAs(UnmanagedType.Bool)]
            public bool fAnyOperationsAborted;
            public IntPtr hNameMappings;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string? lpszProgressTitle;
        }
    }
}

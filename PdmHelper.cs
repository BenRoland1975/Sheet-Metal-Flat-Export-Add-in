using SolidWorks.Interop.sldworks;
using System;
using System.Windows.Forms;
using System.Diagnostics;
using System.IO;

namespace RoesleinAddIn
{
    // SolidWorks enums used in this class
    public enum swCustomInfoType_e
    {
        swCustomInfoText = 30,
        swCustomInfoDate = 60,
        swCustomInfoNumber = 40,
        swCustomInfoYesNo = 20
    }

    public enum swCustomPropertyAddOption_e
    {
        swCustomPropertyOnlyIfNew = 0,
        swCustomPropertyDeleteAndAdd = 1
    }

    /// <summary>
    /// Basic PDM type definitions to allow code to compile without actual PDM dependencies
    /// </summary>
    #region PDM Type Stubs
    // Basic interface stubs that mimic EPDM.Interop.epdm types
    public interface IEdmVault5
    {
        bool IsLoggedIn { get; }
        string Name { get; }
        string CurrentUserName { get; }
        IEdmFile5 GetFileFromPath(string path, out IEdmFolder5 folder);
        void LoginAuto(string vaultName, int timeOut);
        object GetVaultViews();
        object CreateUtility(object utilityType);
        
        // Additional methods needed by Settings.cs and SettingsFormV2.cs
        bool MsgBox(long hwndParent, string message);
        void GetErrorString(int hresult, out string bstrErrorString, out string bstrMostRecentExternallyReferencedFile);
    }
    
    public interface IEdmVault7 : IEdmVault5
    {
        // Inherits all methods from IEdmVault5 plus any additional members
    }
    
    public interface IEdmUserMgr5
    {
        IEdmUser5 GetLoggedInUser();
    }
    
    public interface IEdmFile5
    {
        bool IsLocked { get; }
        int ID { get; }
        object CurrentState { get; }
        object LockedByUser { get; }
        int CurrentVersion { get; }
        string Name { get; }
        
        void LockFile(int folderID, int windowHandle, int flags);
        void UnlockFile(int windowHandle, string comment, int flags = 0);
        void GetFileCopy(int windowHandle, int version, string localPath = null);
        void UndoLockFile(int windowHandle, bool noPrompt = true);
        string GetLocalPath(int folderID);
    }
    
    public interface IEdmFolder5
    {
        int ID { get; }
        string Name { get; }
        int GetFolderFromPath(string path);
        string GetPathFromFolderID(int folderID);
        void AddFile(int parentWndHandle, string filePath, string comment);
    }
    
    public interface IEdmUser5
    {
        string Name { get; }
        int ID { get; }
    }
    
    // Additional interfaces used in Settings.cs
    public interface IEdmState5
    {
        string Name { get; }
    }
    
    // PDM enum types used in Settings.cs and SettingsFormV2.cs
    public enum EdmCmdFlags
    {
        EdmCmd_Nothing = 0,
        EdmCmd_Force = 1,
        EdmCmd_Silent = 2
    }
    
    public enum EdmLockFlag
    {
        EdmLock_Simple = 0,
        EdmLock_GetLatest = 2,
        EdmLock_KeepHCopy = 4
    }
    
    public enum EdmUtility
    {
        EdmUtil_UserMgr = 1
    }
    
    // Empty implementation of IEdmVault5 that doesn't do anything
    public class NullPdmVault : IEdmVault5, IEdmVault7
    {
        public bool IsLoggedIn => false;
        public string Name => "NullVault";
        public string CurrentUserName => System.Environment.UserName;
        
        public IEdmFile5 GetFileFromPath(string path, out IEdmFolder5 folder)
        {
            folder = new NullPdmFolder();
            return new NullPdmFile();
        }
        
        public void LoginAuto(string vaultName, int timeOut) { }
        
        public object GetVaultViews()
        {
            return new string[] { "NullVault" };
        }
        
        public object CreateUtility(object utilityType)
        {
            if (utilityType is EdmUtility utility && utility == EdmUtility.EdmUtil_UserMgr)
            {
                return new NullUserMgr();
            }
            return null;
        }
        
        public bool MsgBox(long hwndParent, string message)
        {
            MessageBox.Show(message, "PDM Message", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return true;
        }

        public void GetErrorString(int hresult, out string bstrErrorString, out string bstrMostRecentExternallyReferencedFile)
        {
            bstrErrorString = $"Error code: {hresult}";
            bstrMostRecentExternallyReferencedFile = null;
        }
    }
    
    // Empty implementation of IEdmFile5
    public class NullPdmFile : IEdmFile5
    {
        public bool IsLocked => false;
        public int ID => 0;
        public object CurrentState => new NullPdmState();
        public object LockedByUser => new NullPdmUser();
        public int CurrentVersion => 1;
        public string Name => "NullFile";
        
        public void LockFile(int folderID, int windowHandle, int flags) { }
        public void UnlockFile(int windowHandle, string comment, int flags = 0) { }
        public void GetFileCopy(int windowHandle, int version, string localPath = null) { }
        public void UndoLockFile(int windowHandle, bool noPrompt = true) { }
        public string GetLocalPath(int folderID) { return null; }
    }
    
    // Empty implementation of IEdmFolder5
    public class NullPdmFolder : IEdmFolder5
    {
        public int ID => 0;
        public string Name => "NullFolder";
        
        public int GetFolderFromPath(string path) { return 0; }
        public string GetPathFromFolderID(int folderID) { return "C:\\NullPath"; }
        public void AddFile(int parentWndHandle, string filePath, string comment) { }
    }
    
    // Empty implementation of IEdmUser5
    public class NullPdmUser : IEdmUser5
    {
        public string Name => "NullUser";
        public int ID => 0;
    }
    
    // Empty implementation of IEdmState5
    public class NullPdmState : IEdmState5
    {
        public string Name => "NullState";
    }
    
    // Empty implementation of NullUserMgr
    public class NullUserMgr : IEdmUserMgr5
    {
        public IEdmUser5 GetLoggedInUser()
        {
            return new NullPdmUser();
        }
    }
    #endregion
    
    /// <summary>
    /// Helper class to bypass PDM checkout issues with read-only mode
    /// </summary>
    public static class PdmHelper
    {
        /// <summary>
        /// Create a null PDM vault that doesn't perform real operations
        /// </summary>
        public static IEdmVault5 GetNullVault()
        {
            return new NullPdmVault();
        }
        
        /// <summary>
        /// Show dialog asking the user if they want to proceed in read-only mode
        /// </summary>
        public static bool AskForReadOnlyMode()
        {
            DialogResult result = MessageBox.Show(
                "Failed to check out the file automatically. Would you like to:\n\n" +
                "• Click YES to apply standards WITHOUT checking out (read-only preview)\n" +
                "• Click NO to cancel the operation\n\n" +
                "Note: Read-only mode will show changes but won't save them.",
                "PDM Checkout Failed",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            return result == DialogResult.Yes;
        }

        /// <summary>
        /// Try to add a custom marker property to a SolidWorks model so we know it's in read-only mode
        /// </summary>
        public static void MarkAsReadOnly(ModelDoc2 swModel)
        {
            try
            {
                CustomPropertyManager propMgr = swModel.Extension.CustomPropertyManager[""];
                propMgr.Add3("RoesleinReadOnlyMode", (int)swCustomInfoType_e.swCustomInfoText, "TRUE", (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
            }
            catch { }
        }
        
        /// <summary>
        /// Log message to a specific location for debugging
        /// </summary>
        public static void LogMessage(string message)
        {
            try
            {
                Logger.Log($"[PdmHelper] {message}");
                System.Diagnostics.Debug.WriteLine($"[PdmHelper] {message}");
            }
            catch { }
        }
    }
} 
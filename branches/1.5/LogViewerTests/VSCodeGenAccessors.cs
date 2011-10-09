﻿// ------------------------------------------------------------------------------
//<autogenerated>
//        This code was generated by Microsoft Visual Studio Team System 2005.
//
//        Changes to this file may cause incorrect behavior and will be lost if
//        the code is regenerated.
//</autogenerated>
//------------------------------------------------------------------------------
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LogJointTests
{
[System.Diagnostics.DebuggerStepThrough()]
[System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.VisualStudio.TestTools.UnitTestGeneration", "1.0.0.0")]
internal class BaseAccessor {
    
    protected Microsoft.VisualStudio.TestTools.UnitTesting.PrivateObject m_privateObject;
    
    protected BaseAccessor(object target, Microsoft.VisualStudio.TestTools.UnitTesting.PrivateType type) {
        m_privateObject = new Microsoft.VisualStudio.TestTools.UnitTesting.PrivateObject(target, type);
    }
    
    protected BaseAccessor(Microsoft.VisualStudio.TestTools.UnitTesting.PrivateType type) : 
            this(null, type) {
    }
    
    internal virtual object Target {
        get {
            return m_privateObject.Target;
        }
    }
    
    public override string ToString() {
        return this.Target.ToString();
    }
    
    public override bool Equals(object obj) {
        if (typeof(BaseAccessor).IsInstanceOfType(obj)) {
            obj = ((BaseAccessor)(obj)).Target;
        }
        return this.Target.Equals(obj);
    }
    
    public override int GetHashCode() {
        return this.Target.GetHashCode();
    }
}


[System.Diagnostics.DebuggerStepThrough()]
[System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.VisualStudio.TestTools.UnitTestGeneration", "1.0.0.0")]
internal class LogJoint_ConcatReadingStreamAccessor : BaseAccessor {
    
    protected static Microsoft.VisualStudio.TestTools.UnitTesting.PrivateType m_privateType = new Microsoft.VisualStudio.TestTools.UnitTesting.PrivateType("logjoint", "LogJoint.ConcatReadingStream");
    
    internal LogJoint_ConcatReadingStreamAccessor(object target) : 
            base(target, m_privateType) {
    }
    
    internal long Length {
        get {
            long ret = ((long)(m_privateObject.GetProperty("Length")));
            return ret;
        }
    }
    
    internal long Position {
        get {
            long ret = ((long)(m_privateObject.GetProperty("Position")));
            return ret;
        }
        set {
            m_privateObject.SetProperty("Position", value);
        }
    }
    
    internal bool CanRead {
        get {
            bool ret = ((bool)(m_privateObject.GetProperty("CanRead")));
            return ret;
        }
    }
    
    internal bool CanSeek {
        get {
            bool ret = ((bool)(m_privateObject.GetProperty("CanSeek")));
            return ret;
        }
    }
    
    internal bool CanWrite {
        get {
            bool ret = ((bool)(m_privateObject.GetProperty("CanWrite")));
            return ret;
        }
    }
    
    internal long position {
        get {
            long ret = ((long)(m_privateObject.GetField("position")));
            return ret;
        }
        set {
            m_privateObject.SetField("position", value);
        }
    }
    
    internal int Read(byte[] buffer, int offset, int count) {
        object[] args = new object[] {
                buffer,
                offset,
                count};
        int ret = ((int)(m_privateObject.Invoke("Read", new System.Type[] {
                    typeof(byte).MakeArrayType(),
                    typeof(int),
                    typeof(int)}, args)));
        return ret;
    }
    
    internal long Seek(long offset, global::System.IO.SeekOrigin origin) {
        object[] args = new object[] {
                offset,
                origin};
        long ret = ((long)(m_privateObject.Invoke("Seek", new System.Type[] {
                    typeof(long),
                    typeof(global::System.IO.SeekOrigin)}, args)));
        return ret;
    }
    
    internal void Flush() {
        object[] args = new object[0];
        m_privateObject.Invoke("Flush", new System.Type[0], args);
    }
    
    internal void SetLength(long value) {
        object[] args = new object[] {
                value};
        m_privateObject.Invoke("SetLength", new System.Type[] {
                    typeof(long)}, args);
    }
    
    internal void Write(byte[] buffer, int offset, int count) {
        object[] args = new object[] {
                buffer,
                offset,
                count};
        m_privateObject.Invoke("Write", new System.Type[] {
                    typeof(byte).MakeArrayType(),
                    typeof(int),
                    typeof(int)}, args);
    }
    
    internal void ThrowModificationAttemptException() {
        object[] args = new object[0];
        m_privateObject.Invoke("ThrowModificationAttemptException", new System.Type[0], args);
    }
    
    internal static global::System.IO.Stream CreatePrivate() {
        Microsoft.VisualStudio.TestTools.UnitTesting.PrivateObject priv_obj = new Microsoft.VisualStudio.TestTools.UnitTesting.PrivateObject("logjoint", "LogJoint.ConcatReadingStream", new object[0]);
        return ((global::System.IO.Stream)(priv_obj.Target));
    }
}
[System.Diagnostics.DebuggerStepThrough()]
[System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.VisualStudio.TestTools.UnitTestGeneration", "1.0.0.0")]
internal class LogJoint_SimpleFileMediaAccessor : BaseAccessor {
    
    protected static Microsoft.VisualStudio.TestTools.UnitTesting.PrivateType m_privateType = new Microsoft.VisualStudio.TestTools.UnitTesting.PrivateType("logjoint", "LogJoint.SimpleFileMedia");
    
    internal LogJoint_SimpleFileMediaAccessor(object target) : 
            base(target, m_privateType) {
    }
    
    internal global::LogJointTests.LogJoint_LogMedia_IFileSystemAccessor fileSystem {
        get {
            object _ret_val = m_privateObject.GetField("fileSystem");
            global::LogJointTests.LogJoint_LogMedia_IFileSystemAccessor _ret = null;
            if ((_ret_val != null)) {
                _ret = new global::LogJointTests.LogJoint_LogMedia_IFileSystemAccessor(_ret_val);
            }
            global::LogJointTests.LogJoint_LogMedia_IFileSystemAccessor ret = _ret;
            return ret;
        }
        set {
            m_privateObject.SetField("fileSystem", value);
        }
    }
    
    internal string fileName {
        get {
            string ret = ((string)(m_privateObject.GetField("fileName")));
            return ret;
        }
        set {
            m_privateObject.SetField("fileName", value);
        }
    }
    
    internal global::System.IO.Stream stream {
        get {
            global::System.IO.Stream ret = ((global::System.IO.Stream)(m_privateObject.GetField("stream")));
            return ret;
        }
        set {
            m_privateObject.SetField("stream", value);
        }
    }
    
    internal global::System.DateTime lastModified {
        get {
            global::System.DateTime ret = ((global::System.DateTime)(m_privateObject.GetField("lastModified")));
            return ret;
        }
        set {
            m_privateObject.SetField("lastModified", value);
        }
    }
    
    internal long size {
        get {
            long ret = ((long)(m_privateObject.GetField("size")));
            return ret;
        }
        set {
            m_privateObject.SetField("size", value);
        }
    }
    
    internal global::System.IO.Stream DataStream {
        get {
            global::System.IO.Stream ret = ((global::System.IO.Stream)(m_privateObject.GetProperty("DataStream")));
            return ret;
        }
    }
    
    internal global::System.DateTime LastModified {
        get {
            global::System.DateTime ret = ((global::System.DateTime)(m_privateObject.GetProperty("LastModified")));
            return ret;
        }
    }
    
    internal long Size {
        get {
            long ret = ((long)(m_privateObject.GetProperty("Size")));
            return ret;
        }
    }
    
    internal static object CreatePrivate(global::LogJoint.IConnectionParams connectParams, global::LogJoint.MediaInitParams p) {
        object[] args = new object[] {
                connectParams,
                p};
        Microsoft.VisualStudio.TestTools.UnitTesting.PrivateObject priv_obj = new Microsoft.VisualStudio.TestTools.UnitTesting.PrivateObject("logjoint", "LogJoint.SimpleFileMedia", new System.Type[] {
                    typeof(global::LogJoint.IConnectionParams),
                    typeof(global::LogJoint.MediaInitParams)}, args);
        return priv_obj.Target;
    }
    
    internal static object CreatePrivate(global::LogJointTests.LogJoint_LogMedia_IFileSystemAccessor fileSystem, global::LogJoint.IConnectionParams connectParams, global::LogJoint.MediaInitParams p) {
        object fileSystem_val_target = null;
        if ((fileSystem != null)) {
            fileSystem_val_target = fileSystem.Target;
        }
        object[] args = new object[] {
                fileSystem_val_target,
                connectParams,
                p};
        Microsoft.VisualStudio.TestTools.UnitTesting.PrivateType target = new Microsoft.VisualStudio.TestTools.UnitTesting.PrivateType("logjoint", "LogJoint.LogMedia.IFileSystem");
        Microsoft.VisualStudio.TestTools.UnitTesting.PrivateObject priv_obj = new Microsoft.VisualStudio.TestTools.UnitTesting.PrivateObject("logjoint", "LogJoint.SimpleFileMedia", new System.Type[] {
                    target.ReferencedType,
                    typeof(global::LogJoint.IConnectionParams),
                    typeof(global::LogJoint.MediaInitParams)}, args);
        return priv_obj.Target;
    }
    
    internal static object CreatePrivate(global::LogJointTests.LogJoint_LogMedia_IFileSystemAccessor fileSystem, string fileName) {
        object fileSystem_val_target = null;
        if ((fileSystem != null)) {
            fileSystem_val_target = fileSystem.Target;
        }
        object[] args = new object[] {
                fileSystem_val_target,
                fileName};
        Microsoft.VisualStudio.TestTools.UnitTesting.PrivateType target = new Microsoft.VisualStudio.TestTools.UnitTesting.PrivateType("logjoint", "LogJoint.LogMedia.IFileSystem");
        Microsoft.VisualStudio.TestTools.UnitTesting.PrivateObject priv_obj = new Microsoft.VisualStudio.TestTools.UnitTesting.PrivateObject("logjoint", "LogJoint.SimpleFileMedia", new System.Type[] {
                    target.ReferencedType,
                    typeof(string)}, args);
        return priv_obj.Target;
    }
    
    internal void Init() {
        object[] args = new object[0];
        m_privateObject.Invoke("Init", new System.Type[0], args);
    }
    
    internal void Update() {
        object[] args = new object[0];
        m_privateObject.Invoke("Update", new System.Type[0], args);
    }
    
    internal void Dispose() {
        object[] args = new object[0];
        m_privateObject.Invoke("Dispose", new System.Type[0], args);
    }
}
[System.Diagnostics.DebuggerStepThrough()]
[System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.VisualStudio.TestTools.UnitTestGeneration", "1.0.0.0")]
internal class LogJoint_LogMedia_IFileSystemAccessor : BaseAccessor {
    
    protected static Microsoft.VisualStudio.TestTools.UnitTesting.PrivateType m_privateType = new Microsoft.VisualStudio.TestTools.UnitTesting.PrivateType("logjoint", "LogJoint.LogMedia.IFileSystem");
    
    internal LogJoint_LogMedia_IFileSystemAccessor(object target) : 
            base(target, m_privateType) {
    }
    
    internal global::System.IO.Stream OpenFile(string fileName, global::System.IO.FileMode mode, global::System.IO.FileAccess access, global::System.IO.FileShare share) {
        object[] args = new object[] {
                fileName,
                mode,
                access,
                share};
        global::System.IO.Stream ret = ((global::System.IO.Stream)(m_privateObject.Invoke("OpenFile", new System.Type[] {
                    typeof(string),
                    typeof(global::System.IO.FileMode),
                    typeof(global::System.IO.FileAccess),
                    typeof(global::System.IO.FileShare)}, args)));
        return ret;
    }
    
    internal string[] GetFiles(string path, string searchPattern) {
        object[] args = new object[] {
                path,
                searchPattern};
        string[] ret = ((string[])(m_privateObject.Invoke("GetFiles", new System.Type[] {
                    typeof(string),
                    typeof(string)}, args)));
        return ret;
    }
    
    internal global::System.DateTime GetLastWriteTime(string fileName) {
        object[] args = new object[] {
                fileName};
        global::System.DateTime ret = ((global::System.DateTime)(m_privateObject.Invoke("GetLastWriteTime", new System.Type[] {
                    typeof(string)}, args)));
        return ret;
    }
}
}
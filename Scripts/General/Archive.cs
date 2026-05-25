using System;
using System.Threading;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using LitJson;
using UnityEngine;
using WarField;
using SCD = SysCfgDefines;

namespace WarArchive
{
    //记录游戏存档的信息
    public class ArcSaveRecord
    {
        public string p_displayName;
        public string p_folderName;
        public string p_createdAt; //创建的时间 utc时间,显示的时候需要转成local time
        public string p_updatedAt; //更新的时间 utc时间,显示的时候需要转成local time
        public string p_version;
        public int p_index; //存档在UI中显示的序号
    }

    //存档管理,包括游戏存档以及一些游戏的系统设置
    public class Archive
    {
#region public parameters

#endregion

#region private parameters

        static private Archive _instance;
        private string _archiveFolderPath; //存档路径
        private string _arcSaveRecordPath; //记录存档详情的文件路径

        private List<ArcSaveRecord> _saveRecords;
        private ArcSaveRecord _curArchive = null;
        private string _curArchiveFolderPath; //当前存档对应文件夹的完整路径
#endregion

#region private parameters' get set

#endregion

#region public functions

        static public Archive Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new Archive();
                return _instance;
            }
        }

        //建立一个新的存档
        public ArcSaveRecord CreateArchive(string displayName, int index)
        {
            if (HasArchive(displayName))
            {
                return null;
            }

            var folderName = Guid.NewGuid().ToString("N");
            var fullPath = Path.Combine(_archiveFolderPath, folderName);
            Directory.CreateDirectory(fullPath);

            var now = DateTime.UtcNow.ToString("u");

            var record = new ArcSaveRecord
            {
                p_displayName = displayName,
                p_folderName = folderName,
                p_createdAt = now,
                p_updatedAt = now,
                p_version = "1.0.0",
                p_index = index,
            };

            _saveRecords.Add(record);
            UpdateArcSaveRecordList();
            return record;
        }

        public bool DeleteArchive(ArcSaveRecord archive)
        {
            if(archive == null)
                return false;
            if(_saveRecords.Contains(archive) == false)
                return false;

            var folderPath = Path.Combine(_archiveFolderPath, archive.p_folderName);
            if (Directory.Exists(folderPath))
                Directory.Delete(folderPath, true);

            _saveRecords.Remove(archive);
            UpdateArcSaveRecordList();
            return true;
        }

        //修改存档名字
        public bool ChangeArchiveName(ArcSaveRecord record, string newName)
        {
            if(record == null || string.IsNullOrEmpty(newName) == true)
                return false;

            if (HasArchive(newName)) //检查重名
            {
                return false;
            }

            record.p_displayName = newName;
            UpdateTimestamp(record);
            UpdateArcSaveRecordList();
            return true;
        }

        //运行存档
        public bool EnterArchive(ArcSaveRecord archive)
        {
            if(archive == null)
                return false;

            if (_curArchive != null && _curArchive == archive)
            {
                GameLogger.LogError($"Need to exit the runnning archive {_curArchive} then can run the new archive");
                return false;
            }

            _curArchive = archive;
            _curArchiveFolderPath = Path.Combine(_archiveFolderPath, _curArchive.p_folderName);
            return true;
        }

        public bool EnterArchive(string arcName)
        {
            var record = _saveRecords.Find(r => r.p_displayName == arcName);
            if (record == null)
            {
                GameLogger.LogError($"Can not find the archive {arcName}");
                return false;
            }

            if (_curArchive != record) //not the current archive
            {
                _curArchive = record;
                _curArchiveFolderPath = Path.Combine(_archiveFolderPath, _curArchive.p_folderName);
            }

            return true;
        }

        //退出存档
        public void ExitArchive()
        {
            _curArchive = null;
            _curArchiveFolderPath = "";
        }

        //读取一个当前存档中的文件
        public T LoadArchiveFile<T>(string fileName)
        {
            if (_curArchive == null)
            {
                GameLogger.LogError("[LoadArchive] Not choose archive yet");
                return default(T);
            }

            return JsonUtils.LoadJsonData<T>(_curArchiveFolderPath, fileName);
        }

        //将data保存到存档中的文件
        //文件不存在会创建
        public bool SaveArchiveFile(string fileName, object data)
        {
            if (_curArchive == null)
            {
                GameLogger.LogError("[LoadArchive] Not choose archive yet");
                return false;
            }

            if(JsonUtils.SaveJsonData(_curArchiveFolderPath, fileName, data) == false)
                return false;
            UpdateTimestamp(_curArchive);
            return true;
        }

        public List<ArcSaveRecord> GetAllSaveRecords()
        {
            return _saveRecords;
        }

#endregion

#region private functions

        //private 的构造函数,不让外部调用
        private Archive()
        {
            _archiveFolderPath = Path.Combine(Application.persistentDataPath, "Archive");
            _arcSaveRecordPath = Path.Combine(_archiveFolderPath, "ArcSaveRecords.json");

            if (!Directory.Exists(_archiveFolderPath))
                Directory.CreateDirectory(_archiveFolderPath);

            LoadArcSaveRecordList();
        }

        private bool HasArchive(string arcName)
        {
            return _saveRecords.Exists(r => r.p_displayName == arcName);
        }

        //更新存档的更新时间
        private void UpdateTimestamp(ArcSaveRecord record)
        {
            if (record != null)
            {
                record.p_updatedAt = DateTime.UtcNow.ToString("u");
                UpdateArcSaveRecordList();
            }
        }

        private void LoadArcSaveRecordList()
        {
            _saveRecords = JsonUtils.LoadJsonData<List<ArcSaveRecord>>(_arcSaveRecordPath);

            if(_saveRecords == null) //文件还没有创建
            {
                _saveRecords = new List<ArcSaveRecord>();
                UpdateArcSaveRecordList(); //创建
            }
        }

        private void UpdateArcSaveRecordList()
        {
            JsonUtils.SaveJsonData(_arcSaveRecordPath, _saveRecords);//直接覆盖整个文件
        }
#endregion
    }
}

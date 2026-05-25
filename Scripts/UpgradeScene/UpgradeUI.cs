using System.Collections;
using System.Collections.Generic;
using System.Xml;
using UnityEngine;
using UnityEngine.UI;

namespace UpgradeScene
{
    using UUD = UpgradeUIDefines;

    public class UpgradeUI : MonoBehaviour
    {
        #region public parameters
        static public UpgradeUI Instance;
        #endregion

        #region private parameters
        [SerializeField] private Button[] _upgradeBts;
        [SerializeField] private GameObject[] _panels;
        [SerializeField] private RectTransform _upgradeField;

        private List<UpgradeConf>[] _upgradeConfs;
        private bool _beInited = false;
        private List<UpgradeItem>[] _upgradeItems;
        private UUD.UpgradeTypes _curShowType = UUD.UpgradeTypes.MIN;
        #endregion

        #region private parameters' get set

        #endregion

        #region Unity callbacks
        private void Awake()
        {
            if(Instance != null)
            {
                Destroy(gameObject);
            }
            Instance = this;

            _upgradeConfs = new List<UpgradeConf>[(int)UUD.UpgradeTypes.MAX];
            for (int i = 1; i < (int)UUD.UpgradeTypes.MAX; i++) //ignore the first one (index = 0)
            {
                _upgradeConfs[i] = new List<UpgradeConf>();
            }

            _upgradeItems = new List<UpgradeItem>[(int)UUD.UpgradeTypes.MAX];
            for (int i = 1; i < (int)UUD.UpgradeTypes.MAX; i++) //ignore the first one (index = 0)
            {
                _upgradeItems[i] = new List<UpgradeItem>();
            }
            ReadUpgradConfs();
            for (int i = 1; i < _panels.Length; i++)
            {
                _panels[i].SetActive(false);
            }

            SetCurUpgradePanel(UUD.UpgradeTypes.SOLDIERUPGRAD);
            _beInited = true;
        }

        private void Start()
        {
            for(int i = 1;i<_upgradeBts.Length;i++)
            {
                FindUpgradeItems(_panels[i].transform, (UUD.UpgradeTypes)i);
                FindUpgradeLines(_panels[i].transform, (UUD.UpgradeTypes)i);
            }
           // Archive.Instance.SetCurArchive("abc");
            //Archive.Instance.LoadUpgradesFromArchive();
        }

        public void OnButton(int type)
        {
            SetCurUpgradePanel((UUD.UpgradeTypes)type);
        }

        public void UpdateItemStatusByArchive(int type, string name)
        {
            if (type <= (int)UUD.UpgradeTypes.MIN || type >= (int)UUD.UpgradeTypes.MAX)
                return;

            List<UpgradeItem> list = _upgradeItems[type];
            foreach(UpgradeItem item in list)
            {
                if (item.gs_name == name)
                {
                    item.SetUpgradedByArchive();
                    return;
                }
            }
        }
        #endregion

        #region private parameters' get set
        public bool gs_beInited
        {
            get { return _beInited; }
        }
        #endregion

        #region public functions

        public UpgradeConf GetUpgradeConf(UUD.UpgradeTypes type, string name)
        {
            if(type <= UUD.UpgradeTypes.MIN || type >= UUD.UpgradeTypes.MAX)
                return null;
            List<UpgradeConf> list = _upgradeConfs[(int)type];
            int len = list.Count;
            for (int i = 0; i < len; i++)
            {
                if (list[i].p_name == name)
                    return list[i];
            }
            return null;
        }

        public List<UpgradeConf> GetUpgradeConfs(UUD.UpgradeTypes type)
        {
            if (type <= UUD.UpgradeTypes.MIN || type >= UUD.UpgradeTypes.MAX)
                return null;

            return _upgradeConfs[(int)type];
        }

        //from a screen pos to get a upgrade item that this pos is in
        public UpgradeItem GetUpgradeItemPosIn(Vector2 pos, UUD.UpgradeTypes type)
        {
            if (type <= UUD.UpgradeTypes.MIN || type >= UUD.UpgradeTypes.MAX)
                return null;

            List<UpgradeItem> list = _upgradeItems[(int)type];
            for (int i = 0; i < list.Count; i++)
            {
                if(list[i].IsPosIn(pos) == true)
                    return list[i];
            }
            return null;
        }
        #endregion

        #region private functions
        private void ReadUpgradConfs()
        {
            XmlDocument xmlDocument = Utils.LoadXmlFile("Conf/Upgrade/UpgradeConf");
            if (xmlDocument == null)
                return;

            XmlNodeList nodeList = xmlDocument.SelectSingleNode("upgradeConf").ChildNodes;
            for (int i = 0; i < nodeList.Count; i++)
            {
                if (nodeList[i].NodeType == XmlNodeType.Comment)
                    continue;

                XmlElement tmp = (XmlElement)nodeList[i];
                switch(tmp.Name)
                {
                    case "soldier":
                        for (int j = 0; j < nodeList[i].ChildNodes.Count; j++)
                        {
                            SoldierUpgradeConf conf = new SoldierUpgradeConf();
                            conf.p_name = ((XmlElement)nodeList[i].ChildNodes[j]).GetAttribute("name");
                            conf.p_icon = ((XmlElement)nodeList[i].ChildNodes[j]).GetAttribute("icon");
                            AddSoldierUpgradeConf(conf, nodeList[i].ChildNodes[j]);
                        }
                        break;
                    case "hero":

                        break;
                    case "building":

                        break;
                    case "mine":

                        break;
                    case "card":

                        break;
                    default:
                        break;
                }
            }
        }

        private void AddSoldierUpgradeConf(SoldierUpgradeConf conf, XmlNode xml)
        {
            for (int i = 0; i < xml.ChildNodes.Count; i++)
            {
                XmlElement tmp = (XmlElement)xml.ChildNodes[i];
                switch (tmp.Name)
                {
                    case "description":
                        conf.p_description = tmp.GetAttribute("value");
                        break;
                    case "target":
                        conf.p_troop = int.Parse(tmp.GetAttribute("troop"));
                        break;
                    case "attribut":
                        conf.p_attribute = tmp.GetAttribute("name");
                        conf.p_value = tmp.GetAttribute("value");
                        break;
                    default:
                        break;
                }
            }
            _upgradeConfs[(int)UUD.UpgradeTypes.SOLDIERUPGRAD].Add(conf);
        }

        private void FindUpgradeItems(Transform parent, UUD.UpgradeTypes type)
        {
            foreach (Transform child in parent)
            {
                if (child.name.Contains("UpgradeItem"))
                {
                    UpgradeItem item = child.GetComponent<UpgradeItem>();
                    if(item != null)
                    {
                        _upgradeItems[(int)type].Add(item);
                        item.Init(type);
                    }
                }

                FindUpgradeItems(child, type);
            }
        }

        private void FindUpgradeLines(Transform parent, UUD.UpgradeTypes type)
        {
            foreach (Transform child in parent)
            {
                if (child.name.Contains("UpgradeLine"))
                {
                    UpgradeLine item = child.GetComponent<UpgradeLine>();
                    if (item != null)
                        item.Init(type);
                }

                FindUpgradeLines(child, type);
            }
        }

        private void SetCurUpgradePanel(UUD.UpgradeTypes type)
        {
            if (type <= UUD.UpgradeTypes.MIN || type >= UUD.UpgradeTypes.MAX)
                return;

            if (_curShowType == type)
                return;

            if(_curShowType != UUD.UpgradeTypes.MIN)
                _panels[(int)_curShowType].SetActive(false);
            _curShowType = type;
            _panels[(int)_curShowType].SetActive(true);
        }

        #endregion
    }
}


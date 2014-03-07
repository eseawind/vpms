using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using vpms.vms;
using WG3000_COMM.Core;
using System.IO;

namespace vpms
{
    public partial class FrmMain : Form
    {
        private wgWatchingService service = new wgWatchingService();
        private RecordServcieService websvc = new RecordServcieService();
        private FileStream fs = new FileStream("vms.log", FileMode.OpenOrCreate | FileMode.Append, FileAccess.Write);
        private StreamWriter sw;
        System.Collections.Hashtable map = new System.Collections.Hashtable();//存放控制器读卡器映射

        public FrmMain()
        {
            InitializeComponent();
            this.OnStart();
        }

        protected void OnStart()
        {
            string uuid = Guid.NewGuid().ToString();
            try
            {
                sw = new StreamWriter(this.fs);

                Dictionary<int, wgMjController> controllers = new Dictionary<int, wgMjController>();
                GetControllerListRequest r = new GetControllerListRequest();
                Controller[] list = websvc.GetControllerList(r);
                if (list != null)
                {
                    foreach (Controller s in list)
                    {
                        log(uuid, "控制器: " + s.sn + " " + s.ip);
                        sw.Flush();

                        wgMjController ctrl = new wgMjController();
                        ctrl.ControllerSN = System.Int32.Parse(s.sn);
                        ctrl.IP = s.ip;
                        ctrl.PORT = 60000;
                        controllers.Add(ctrl.ControllerSN, ctrl);
                    }
                }
                else
                {
                    log(uuid, "异常: 请求控制器列表返回null");
                }

                service.EventHandler += new OnEventHandler(evtNewInfoCallBack);
                service.WatchingController = controllers;
            }
            catch (Exception e)
            {
                log(uuid, "异常: " + e.Message);
            }
        }

        private void evtNewInfoCallBack(string recd)
        {
            string uuid = Guid.NewGuid().ToString();
            wgMjControllerSwipeRecord rec = new wgMjControllerSwipeRecord(recd);
            onEvent(rec.ControllerSN, rec.CardID, rec.ReadDate, rec.ReaderNo, uuid);
        }

        private void log(string uuid, string log)
        {
            sw.WriteLine(uuid + " 系统时间=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + log);
        }

        private void onEvent(uint ControllerSN, uint CardID, DateTime ReadDate, byte ReaderNo, string uuid)
        {
            log(uuid, "事件: 读卡时间=" + ReadDate.ToString("yyyy-MM-dd HH:mm:ss") + " 读卡器=" + ReaderNo + " 卡号=" + CardID +
                " 控制器=" + (int)ControllerSN);
            if (CardID > 1)
            {
                DateTime dt;

                if (map.ContainsKey("" + CardID))
                {
                    dt = (DateTime)map["" + CardID];
                }
                else
                {
                    dt = DateTime.Now.AddDays(-1); //如果没有信息就相当于一天前刷过卡
                }

                log(uuid, "读卡: 读卡时间=" + ReadDate.ToString("yyyy-MM-dd HH:mm:ss") + " 读卡器=" + ReaderNo + " 卡号=" + CardID +
                    " 控制器=" + (int)ControllerSN);
                // 如果上次读卡时间 距离本次读卡时间超过5秒 则应该记录本次刷卡
                if (dt.AddSeconds(30) < ReadDate)
                {
                    try
                    {
                        RecordRequest req = new RecordRequest();
                        req.cardNumber = "" + CardID;
                        req.controllerSn = "" + ControllerSN;
                        req.readerNumber = "" + ReaderNo + "M" + uuid + "M" + ReadDate.ToString("yyyy-MM-dd HH:mm:ss");

                        log(uuid, "调用: 读卡时间=" + ReadDate.ToString("yyyy-MM-dd HH:mm:ss") + " 读卡器=" + ReaderNo + " 卡号=" + CardID +
                            " 控制器=" + (int)ControllerSN);

                        RecordResponse res = websvc.Record(req);

                        log(uuid, "返回: 读卡时间=" + ReadDate.ToString("yyyy-MM-dd HH:mm:ss") + " 读卡器=" + ReaderNo + " 卡号=" + CardID +
                            " 控制器=" + (int)ControllerSN + " 返回命令=" + res.command + " 杆号=" + res.gateNumber);

                        if (res.command.ToLower().Equals("open"))
                        {
                            sw.Flush();
                            service.WatchingController[(int)ControllerSN].RemoteOpenDoorIP(Int32.Parse(res.gateNumber));//抬杆
                            log(uuid, "抬杆: 读卡时间=" + ReadDate.ToString("yyyy-MM-dd HH:mm:ss") + " 读卡器=" + ReaderNo + " 卡号=" + CardID +
                                " 控制器=" + (int)ControllerSN + " 返回命令=" + res.command + " 杆号=" + res.gateNumber);
                        }
                    }
                    catch (Exception e)
                    {
                        log(uuid, "异常: " + e.ToString());
                    }
                }

                map["" + CardID] = ReadDate;
            }
            sw.WriteLine("");
            sw.Flush();
        }

        protected void OnStop()
        {
            sw.Close();
            fs.Close();
        }

        private void btnSend_Click(object sender, EventArgs e)
        {
            try
            {
                onEvent(UInt32.Parse(txtController.Text), UInt32.Parse(txtCardId.Text),
                    DateTime.Now, Byte.Parse(txtReader.Text), "");
            }
            catch (Exception ee)
            {
                MessageBox.Show(ee.Message);
            }
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            this.Close();
            System.Environment.Exit(0);
        }
    }
}

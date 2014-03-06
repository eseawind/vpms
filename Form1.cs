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
        private FileStream fs = new FileStream("vms.log", FileMode.Create);
        private StreamWriter sw;
        System.Collections.Hashtable map = new System.Collections.Hashtable();//存放控制器读卡器映射

        public FrmMain()
        {
            InitializeComponent();
            this.OnStart();
        }

        protected void OnStart()
        {
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
                        sw.WriteLine(s.sn + " " + s.ip);
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
                    MessageBox.Show("请求控制器列表返回null");
                }

                service.EventHandler += new OnEventHandler(evtNewInfoCallBack);
                service.WatchingController = controllers;
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
            }
        }

        private void evtNewInfoCallBack(string recd)
        {
            sw.WriteLine("raw:"+recd+"\r\n");
            wgMjControllerSwipeRecord rec = new wgMjControllerSwipeRecord(recd);
            onEvent(rec.ControllerSN, rec.CardID, rec.ReadDate, rec.ReaderNo);
        }

        private void onEvent(uint ControllerSN, uint CardID, DateTime ReadDate, byte ReaderNo)
        {
            sw.WriteLine("EVENT: Reader=" + ReaderNo + " Date=" + ReadDate.ToString("yyyy-MM-dd HH:mm:ss") + " CardId=" + CardID +
                " ControllerSN=" + (int)ControllerSN);
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

                sw.WriteLine("CARDID=1: Reader=" + ReaderNo + " Date=" + ReadDate.ToString("yyyy-MM-dd HH:mm:ss") + " CardId=" + CardID +
                    " ControllerSN=" + (int)ControllerSN);
                // 如果上次读卡时间 距离本次读卡时间超过5秒 则应该记录本次刷卡
                if (dt.AddSeconds(10) < ReadDate)
                {
                    try
                    {
                        RecordRequest req = new RecordRequest();
                        req.cardNumber = "" + CardID;
                        req.controllerSn = "" + ControllerSN;
                        req.readerNumber = "" + ReaderNo;
                        
                        sw.WriteLine("WS: Reader=" + ReaderNo + " Date=" + ReadDate.ToString("yyyy-MM-dd HH:mm:ss") + " CardId=" + CardID +
                            " ControllerSN=" + (int)ControllerSN );

                        RecordResponse res = websvc.Record(req);

                        sw.WriteLine("G: COMMD=" + res.command + " Reader=" + ReaderNo + " Date=" + ReadDate.ToString("yyyy-MM-dd HH:mm:ss") + " CardId=" + CardID +
                            " ControllerSN=" + (int)ControllerSN + " Gan=" + res.gateNumber);

                        if (res.command.ToLower().Equals("open"))
                        {
                            sw.Flush();
                            service.WatchingController[(int)ControllerSN].RemoteOpenDoorIP(Int32.Parse(res.gateNumber));//抬杆
                        }
                    }
                    catch (Exception e)
                    {
                        sw.WriteLine(e);
                        //MessageBox.Show(e.Message);
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
                    DateTime.Now, Byte.Parse(txtReader.Text));
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

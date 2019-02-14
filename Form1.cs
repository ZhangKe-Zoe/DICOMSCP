using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Threading;
using System.Net.Sockets;
using System.Net;
using System.IO;

namespace DICOMSCP
{
    public partial class DICOMSCP : Form
    {
        public DICOMSCP()
        {
            InitializeComponent();
        }

        Thread threadWatch = null;//负责监听客户端的线程
        Thread threadConnection = null;//负责监听客户端通信的线程

        TcpListener tcpListener = null;//负责监听客户端

        public BinaryReader br { get; private set; } //读取信息
        public BinaryWriter bw { get; private set; } //写入信息

        //创建一个负责和客户端通信tcpclient
        List<Thread> dictThread = new List<Thread>();
        List<TcpClient> tcpConnections = new List<TcpClient>();

        //声明配置信息
        string IP, Port;
        byte[] CallingTitle;
        byte[] CalledTitle;
        IPAddress ipaddress;
        IPEndPoint endpoint;
        string ced, cing;

        //循环读取pdutype标志
        bool f = true;
        //判断网络流通断标志
        bool nets = false;
        //开始按钮
        private void btnStart_Click(object sender, EventArgs e)
        {
            // 服务端发送信息 需要1个IP地址和端口号
            IP = "0.0.0.0";
            if (tbPort.Text != "") Port = tbPort.Text;
            else//调试
            {
                Port = "104";
                tbPort.Text = Port;
            }
            //获取主叫，被叫信息
            if (tbCed.Text == "" && tbCing.Text == "")
            {
                ced = "USSTSCP";
                cing = "USSTSCU";
                tbCed.Text = ced;
                tbCing.Text = cing;
            }
            else
            {
                ced = tbCed.Text;
                cing = tbCing.Text;
            }
            //二进制
            CalledTitle = Encoding.Default.GetBytes(ced);
            CallingTitle = Encoding.Default.GetBytes(cing);


            //显示配置信息
            rtbLog.AppendText("\nProperties:\nIp:" + IP + "\t" + "Port:" + Port + "\t" + "CalledTitle:" + ced + "\t" + "CallingTitle:" + cing + "\n\n");

            try
            {   //将IP地址和端口号绑定到网络节点endpoint上 
                ipaddress = IPAddress.Parse(IP.Trim());
                endpoint = new IPEndPoint(ipaddress, int.Parse(Port.Trim()));
                //实例化TcpListener
                tcpListener = new TcpListener(endpoint);
                tcpListener.Start();
                //创建监听线程
                threadWatch = new Thread(WatchConnecting);
                CheckForIllegalCrossThreadCalls = false;//可跨线程调用
                threadWatch.IsBackground = true;//后台线程
                threadWatch.Start();
                rtbLog.AppendText("Listening on Port" + Port + "\r\n" + "Waiting for invocations from clients..." + "\r\n\n");
            }
            catch (Exception err)
            {

            }

        }
        //关闭连接按钮
        string test;
        private void btnStop_Click(object sender, EventArgs e)
        {

            int i = 0;//计数
            
            //网络流未关闭时，向scu发送RELEASE-RQ
            while (nets)
            {

                if (i < 1)
                {
                    bw.Write(HexStringToByteArray("05 00 0000000A 00 00 00 00"));
                    bw.Flush();
                    rtbLog.AppendText("\nDICOMSCU << A-RELEASE-RQ PDU\n");

                }
                test = br.ReadByte().ToString("x2");
                i++;

                if (test == "04")//读到"04"后网络流结束
                {
                    rtbLog.AppendText("\nDICOMSCU >> A-RELEASE-RP PDU\n");
                    nets=false;
                }
            }

            //结束监听
            if (threadConnection != null)
                threadConnection.Abort();//关闭通信线程
            br.Close();
            bw.Close();
            tcpListener.Stop();
            rtbLog.AppendText("\n\nRelasing connection successfully\r\n");

        }


        // 监听客户端发来的请求
        private void WatchConnecting()
        {
            while (true)  //持续不断监听客户端发来的请求
            {
                try
                {
                    //获取网络流
                    TcpClient tcpConnection = tcpListener.AcceptTcpClient();
                    NetworkStream networkStream = tcpConnection.GetStream();
                    br = new BinaryReader(networkStream);
                    bw = new BinaryWriter(networkStream);
                    nets = true;//修改通信状态
                    //创建一个通信线程 
                    threadConnection = new Thread(ReceiveFromClient);
                    //后台线程
                    threadConnection.IsBackground = true;
                    tcpConnections.Add(tcpConnection);
                    CheckForIllegalCrossThreadCalls = false;
                    //启动线程
                    threadConnection.Start();
                    dictThread.Add(threadConnection);
                }
                catch { break; }

            }
        }

        //存储数据流第一位结果
        byte t;
        string type = null;
        //处理接收的客户端数据
        public void ReceiveFromClient()
        {
            /*
              1 循环读取:先读取一位，判断类型，使用不同方法解码（RQ/AC 和 RJ/RL）
              2 安位循环读取，大于PDUlength或AET时break
              3 根据不同类型构造并返回结果         
              4 循环接收客户端pdu
              */

            while (true)
            {
                try
                {
                    PDU pdu = null;//实例化相应PDU解析类，存储解析并pdu内容
                    f = true;//循环读取第一位标志

                    //循环读第一位，判断类型，符合类型跳出循环，解析剩余内容
                    while (f)
                    {
                        try
                        {
                            t = br.ReadByte();
                            type = t.ToString("x2");
                            switch (type)
                            {
                                case "01": { f = false; pdu = new AAssociateRQ(); break; }//修改标志，跳出循环
                                case "02": { f = false; pdu = new AAssociateAC(); break; }//修改标志，跳出循环
                                case "03": { f = false; pdu = new AAssociateRJ(); break; }//修改标志，跳出循环
                                case "05": { f = false; pdu = new AReleaseRQ(); break; }//修改标志，跳出循环
                                case "06": { f = false; pdu = new AReleaseRP(); break; }//修改标志，跳出循环
                                default: break;
                            }
                        }
                        catch { break; }
                    }

                    //保存pdu类型，并打印
                    pdu.PDUType[0] = t;
                    rtbLog.AppendText(pdu.Log());

                    //读取PDU长度
                    br.ReadByte();//跳过保留位
                    uint length = 0;
                    for (int i = 0; i < 4; i++)//长度占4位
                    {
                        length *= 256;//左移8位
                        length += br.ReadByte();
                    }
                    pdu.PDULength = length;

                    //读取剩余PDU内容
                    byte[] pdu1 = br.ReadBytes((int)length);

                    //分解剩余头部
                    pdu.PDUSplit(pdu1);
                    rtbLog.AppendText("\nRecieved RQ PDU:" + pdu.PDUString(pdu) + "\n\n");


                    //比较AET
                    pdu.pCalledAET = CalledTitle;
                    pdu.pCallingAET = CallingTitle;
                    pdu.AETitle(pdu);


                    //发送结果
                    rtbLog.AppendText("\nSent PDU:\n");
                    foreach (byte i1 in pdu.r)
                    {
                        rtbLog.AppendText(i1.ToString("x2"));
                    }

                    rtbLog.AppendText("\n\n" + pdu.log);

                    bw.Write(pdu.r);
                    bw.Flush();

                    //释放关联,关闭网络流,修改网络流状态标志，跳出循环
                    if (type == "05") { br.Close(); bw.Close(); nets = false; break; }

                    //重置循环解析type标志
                    f = true;
                }
                catch { break; }
            }


        }


        //将十六进制串转换为byte数组
        private byte[] HexStringToByteArray(string s)
        {
            s = s.Replace(" ", "");
            byte[] buffer = new byte[s.Length / 2];
            for (int i = 0; i < s.Length; i += 2)
                buffer[i / 2] = (byte)Convert.ToByte(s.Substring(i, 2), 16);
            return buffer;

        }
    }
}






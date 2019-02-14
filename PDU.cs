using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace DICOMSCP
{
    public abstract class PDU
    {
        //ces
        public string test;
        // PDU类型
        public byte[] PDUType = new byte[1];
        // PDU长度
        public uint PDULength;
        // 协议版本
        public Byte[] ProtocalVersion = new byte[2];
        // 被叫实体标题（SCU)
        public Byte[] uCalledAET = new byte[16];
        public string uced;
        // 主叫实体标题(SCU)
        public Byte[] uCallingAET = new byte[16];
        public string ucing;
        // 被叫实体标题（SCP)
        public Byte[] pCalledAET = new byte[16];
        public string pced;
        // 主叫实体标题(SCP)
        public Byte[] pCallingAET = new byte[16];
        public string pcing;
        //AC/RJ
        //public string result;
        public Byte[] r;
        // 结果
        public byte Result;
        // 源
        public byte Source;
        // 原因
        public byte Reason;
        //保留
        public Byte[] Reserve;
        //上下文
        public Byte[] Context;
        public string context;
        //保存解析后的PDU
        public List<Item> VItems;
        //
        public byte[] App;
        public byte[] Per;
        public byte[] UInfo;
        //解析PDU
        public abstract void PDUSplit(byte[] pdu);
        //比较AETitle
        public abstract PDU AETitle(PDU pdu1);
        //Log
        public abstract string Log();
        //log内容
        public string log;
        //构造保留字节
        public byte[]Vreserve(int Length)
        {
            Reserve = new Byte[Length];

            for (int i = 0; i < Length; i++)
                Reserve[i] = 00;
                
            
            return Reserve;
        }
        //将十六进制串转换为byte数组
        public byte[] HexStringToByteArray(string s)
        {
            s = s.Replace(" ", "");
            byte[] buffer = new byte[s.Length / 2];
            for (int i = 0; i < s.Length; i += 2)
                buffer[i / 2] = (byte)Convert.ToByte(s.Substring(i, 2), 16);
            return buffer;
        }
        //AETs字节数组转字符串
        public void AETsBtoS(PDU pdu)
        {
           uced = Encoding.Default.GetString(pdu.uCalledAET).TrimEnd();
           ucing = Encoding.Default.GetString(pdu.uCallingAET).TrimEnd();
           pced = Encoding.Default.GetString(pdu.pCalledAET).TrimEnd();
           pcing = Encoding.Default.GetString(pdu.pCallingAET).TrimEnd();
        }
        //编码pdu为string
        public string PDUString(PDU pdu)
        {
            string t =BitConverter.ToString( pdu.PDUType);//byte数组转为16进制string
            string pdus;
            AETsBtoS(pdu);
            
            if (t == "01" || t == "02")
            {
                ContextDataSet cd = new ContextDataSet(pdu);
                cd.AppItem();
                cd.PreItem(pdu.PDUType.ToString() == "02");
                cd.UInfoItem();
                pdu.context = cd.decodeContxt();
                pdus = "\nPDUType:" + t + "\tPDULength:" + pdu.PDULength.ToString("x2") + "\tVersion:" + BitConverter.ToString(ProtocalVersion) + "\nCalledTitle:" + uced + "\nCallingTitle:" +ucing + "\nContext:\n" + pdu.context;
            }
            else
            { pdus = "\nPDUType:" + t + "\tPDULength:" + pdu.PDULength.ToString("x2") + "\tResult:" + pdu.Result.ToString("x2") + "\tSource:" + pdu.Source.ToString("x2") + "\tReason:" + pdu.Reason.ToString("x2"); }
            pdus = pdus.Replace("-", "");
            return pdus;
        }
        //ASCII码转换
        public  string converuid(string s)
        {
            byte[] ba = System.Text.ASCIIEncoding.Default.GetBytes(s);
            StringBuilder sb = new StringBuilder();
            foreach (byte b in ba)
            {
                sb.Append(b.ToString("x"));
            }
            string ca = sb.ToString();
            return ca;
        }


    }
    // AAssociateRQ
    public class AAssociateRQ : PDU
    {
        public override PDU AETitle(PDU pdu)
        {
            AETsBtoS(pdu);

            if (uced ==pced && ucing ==pcing )
            {
                /*构造回复AC
                 * 1实例化新的AC类（保存配置信息）
                 * 2调用AC write，传入相应参数ACpdu，返回ACpdu
                 * 3调用AC.r
                 *
                 */
                AAssociateAC ACpdu = new AAssociateAC();

                ACpdu.uCalledAET = pdu.uCalledAET;
                ACpdu.uCallingAET = pdu.uCallingAET;
                ACpdu.VItems = pdu.VItems;
                ACpdu = ACpdu.writeAC(ACpdu);
                pdu.r = ACpdu.r;

                pdu.log = "DICOMSCU << A - ASSOCIATE - AC PDU" + "\r\n";
            }
            else if(uced!=pced)
            {
                pdu.r = HexStringToByteArray("03 00 0000000A 00 02 03 07");
                pdu.log = "DICOMSCU << A - ASSOCIATE - RJ PDU" + "\r\n";
            }
            else if (ucing != pcing)
            {
                pdu.r = HexStringToByteArray("03 00 0000000A 00 02 03 03");
                pdu.log = "DICOMSCU << A - ASSOCIATE - RJ PDU" + "\r\n";
            }
            return pdu;
        }

        public override string Log()
        {
            
            return ("DICOMSCU >> A - ASSOCIATE - RQ PDU" + "\r\n");
            
        }

        public override void PDUSplit(byte[] pdu)
        {
            Context = new Byte[pdu.Length - 68];
            Array.Copy(pdu, 0, ProtocalVersion, 0, 2);
            Array.Copy(pdu, 4, uCalledAET, 0, 16);
            Array.Copy(pdu, 20,uCallingAET, 0, 16);
            Array.Copy(pdu, 68, Context, 0, pdu.Length - 68);//包含32个保留位
        }
        
        
    }
    //Context 解析内容
    public class Item
    {
        public string ItemType = null;
        public int ItemLength = 0;
        public string PCID = null;
        public string Uid = null;
        public string name = null;
        public string SOPName = null;
        public int MaxReLength = 0;
        public string UserInfo = null;

    }
    //解析并存储context
    public class ContextDataSet:PDU
    {
        /*依次解析context：
         * 1 应用上下文条目
         * 2 循环解析表示上下文条目
         * 2 用户上下文条目
         */
        //存储解析过的item
        protected PDU pdu;
        //string context;
        int i = 0;//位移标准
        public Byte[] temp;

        

        public ContextDataSet(PDU pdu)
        {
            this.pdu = pdu;
            pdu.VItems = new List<Item>();
        }
        //解析APPLICATION CONTEXT ITEM
        public void AppItem()
        {

            Item APP = new Item();
           //解析type，length
            TL(APP);
            //UID
            temp = new Byte[APP.ItemLength];
            Array.Copy(pdu.Context, i, temp, 0, temp.Length);
            APP.Uid = Encoding.Default.GetString(temp);
            i +=APP.ItemLength;//指向下一条目
            pdu.VItems.Add(APP);          
        }
        //解析PRESENTATION CONTEXT ITEM
        public void PreItem(bool isAC)
        {
           string t = pdu.Context[i].ToString("x2");
            while (t == "20")//循环读取
            {
                Item Per = new Item();
                TL(Per);
                t = Per.ItemType;
                if (t != "20") { i-=4; break; }//修改判断条件
                Per.PCID = pdu.Context[i].ToString("x2");
                i+=4;//跳过保留字节
                pdu.VItems.Add(Per);
                //ABSTRACT TRANSFER SYNTAX SUB-ITEM,格式同应用上下文条目
                if(!isAC)AppItem();
                //TRANSFER SYNTAX SUB-ITEM,格式同应用上下文条目,有多条，循环判断
                string k = "40";
                while (true)
                {
                    if (k == "40")//符合类型时读取
                    {
                        AppItem();
                        k = pdu.VItems[pdu.VItems.Count() - 1].ItemType;
                    }
                    else//退回
                    {
                        
                        i -= (pdu.VItems[pdu.VItems.Count() - 1].ItemLength+4);
                        pdu.VItems.Remove(pdu.VItems[pdu.VItems.Count() - 1]);
                        
                        break;
                    }
                }
            }
        }

        //解析USER INFORMATION ITEM
        public void UInfoItem()
        {
            Item UI = new Item();
            TL(UI);
            pdu.VItems.Add(UI);
            //3条UserData，2条格式同应用上下文条目
            
            Item UI1 = new Item();
            TL(UI1);
            UI1.MaxReLength = (pdu.Context[i + 1] << 24) + (pdu.Context[i + 2]<<16)+ (pdu.Context[i + 3] << 8) + (pdu.Context[i + 4] );
            i += 4;
            pdu.VItems.Add(UI1);

            AppItem();
            AppItem();
        }
        //公共模板解析type，length
        public void TL(Item it)
        {
            it.ItemType = BitConverter.ToString(pdu.Context, (int)i,1).Replace("-","");
            i++;
            it.ItemLength =( pdu.Context[i+1] <<8 )+pdu.Context[i+2];//跳过两个保留字节
            i += 3;//后移4位
        }
        //解码
        public string decodeContxt()
        {
            string str = "";
            foreach (Item item in pdu.VItems)
            {
                if (item != null)
                {
                    if (str != "") str += "\n";  //两个数据元素之间用换行符分割
                    str += ToString(item);
                }
            }
            return str;
        }
        //返回各字段的字符串，比如用”\t”分割的各字段的值。
        public  string ToString(Item item)
        {
            string str = "\t";//头部
            if(item.ItemType!=null)
            str += "Type: "+item.ItemType + "\t";
            str += "Length: " + item.ItemLength + "\t";
            if(item.PCID!=null)
                str += "Presentation context ID: " + item.PCID + "\t";
            if (item.Uid != null)
                str += "Uid: "+ item.Uid;
            if (item.MaxReLength != 0)
                str += "Maximumlength received:" + item.MaxReLength;
            str += "\t";
            
            return str;
        }

        public override void PDUSplit(byte[] pdu)
        {
            throw new NotImplementedException();
        }

        public override PDU AETitle(PDU pdu1)
        {
            throw new NotImplementedException();
        }

        public override string Log()
        {
            throw new NotImplementedException();
        }
    }




    //AAssociateAC
    public class AAssociateAC : PDU
    {
        public override PDU AETitle(PDU pdu1)
        {
            
            return pdu1;
        }
       //构造AC
       public AAssociateAC writeAC(AAssociateAC ACpdu)
        {
            //构造AC头部
           
            //type
            ACpdu.PDUType = HexStringToByteArray("02");
            // reserve1
            byte[] reser1 = Vreserve(1);
            //unkonw length
            //version
            ACpdu.ProtocalVersion = HexStringToByteArray("0001");
            //reserve2
            byte[] reser2 = Vreserve(2);
            //called ACpdu.uCalledAET
            //calling ACpdu.uCalledAET
            //reserve32
            byte[] reser32 = Vreserve(32);

            //构造context
            ACpdu = writeAPP(ACpdu);
            ACpdu = writeAPer(ACpdu);
            ACpdu = writeUInfo(ACpdu);

            ACpdu.Context = new Byte[ACpdu.App.Length + ACpdu.Per.Length + ACpdu.UInfo.Length];
            Array.Copy(ACpdu.App, 0, ACpdu.Context, 0, ACpdu.App.Length);
            Array.Copy(ACpdu.Per, 0, ACpdu.Context, ACpdu.App.Length, ACpdu.Per.Length);
            Array.Copy(ACpdu.UInfo, 0, ACpdu.Context, ACpdu.App.Length+ ACpdu.Per.Length, ACpdu.UInfo.Length);


            //获取长度，固定头部长度+上下文长度
            ACpdu.PDULength = (uint)(68+ ACpdu.Context.Length);

            //uint转换为byte数组，格式控制4byte
            byte[] pdul = BitConverter.GetBytes(ACpdu.PDULength);
            Array.Reverse(pdul);
            ACpdu.r = new Byte[ACpdu.PDULength + 6];

            //排序AC内容，存入r
            int len = 0;
            Array.Copy(ACpdu.PDUType, 0, r, len, ACpdu.PDUType.Length); len += ACpdu.PDUType.Length;//type
            Array.Copy(reser1, 0, r, len,1); len += 1;//re1
            Array.Copy(pdul, 0, r, len, pdul.Length);len += pdul.Length;//length
            Array.Copy(ACpdu.ProtocalVersion, 0, r, len, ACpdu.ProtocalVersion.Length);len += ACpdu.ProtocalVersion.Length;//version
            Array.Copy(reser2, 0, r, len, 2); len += 2;//re2
            Array.Copy(ACpdu.uCalledAET, 0, r, len, ACpdu.uCalledAET.Length); len += 16;//ced
            Array.Copy(ACpdu.uCallingAET, 0, r, len, ACpdu.uCallingAET.Length); len += 16;//cing
            Array.Copy(reser32, 0, r, len, 32); len += 32;//re32
            Array.Copy(ACpdu.Context, 0, r, len, ACpdu.Context.Length); len += ACpdu.Context.Length;//contex

            return ACpdu;
            
        }
        //应用上下文（固定）
        public AAssociateAC writeAPP(AAssociateAC ACpdu)
        {
            ACpdu.App = HexStringToByteArray("10 00 0015 312E322E3834302E31303030382E332E312E312E31");
            return ACpdu;
        }
        //表示条目
        string per = null;
        int length20;
        string PCID20 = null;

        public AAssociateAC writeAPer(AAssociateAC ACpdu)
        {
            /*根据RQ编写,
            1 遍历VItems取type=20,30,第一个40的item
            2 复制20，40的内容，注意 AC20length=RQ20length-（RQ30length+4）           
            */
            foreach(Item i in ACpdu.VItems)
            {
                if(i.ItemType=="20")
                {
                    per += ("21"+"00");
                    length20 = i.ItemLength;
                    PCID20 = i.PCID;
                   
                }
                if (i.ItemType =="30")
                {
                    //AC20length=RQ20length-（RQ30length+4）
                    length20 -= (i.ItemLength + 4);
                    per += "00";
                    per += Convert.ToString(length20, 16);
                   
                    per += (PCID20 + "000000");

                }
                if (i.ItemType == "40")
                {
                    per+=(i.ItemType + "00");
                    per += "00";
                    per += Convert.ToString(i.ItemLength, 16);
                    per += converuid(i.Uid);
                }
               
            }
            
            per = per.Replace(".", "2E");
            if (per.Length % 2 != 0) per += "0";
            ACpdu.Per = HexStringToByteArray(per);
            return ACpdu;
        }
        //用户信息条目（固定）
        public AAssociateAC writeUInfo(AAssociateAC ACpdu)
        {
            ACpdu.UInfo = HexStringToByteArray("5000003B51000004000080005200001C312E322E3832362E302E312E333638303034332E322E36302E302E315500000F736F66746C696E6B5F6A6474313033");
            return ACpdu;
        }
        //


        public override string Log()
        {
            return ("DICOMSCU << A - ASSOCIATE - AC PDU" + "\r\n");

        }
        public override void PDUSplit(byte[] pdu)
        {
            Context = new Byte[pdu.Length - 68];
            Array.Copy(pdu, 0, ProtocalVersion, 0, 2);
            Array.Copy(pdu, 4, uCalledAET, 0, 16);
            Array.Copy(pdu, 20, uCallingAET, 0, 16);
            Array.Copy(pdu, 68, Context, 0, pdu.Length - 68);//包含32个保留位
        }
    }
    //AAssociateRJ
    public class AAssociateRJ : PDU
    {
        public override PDU AETitle(PDU pdu1)
        {
           
            return pdu1;
        }

        public override string Log()
        {
            return ("DICOMSCU << A - ASSOCIATE - RJ PDU" + "\r\n");

        }
        //分割PDU并存入相应字段
        public override void PDUSplit(byte[] pdu)
        {
            Result = pdu[1];
            Source = pdu[2];
            Reason = pdu[3];
            
        }
    }
    //AReleaseRQ
    public class AReleaseRQ : PDU
    {
        public override PDU AETitle(PDU pdu1)
        {
           r = HexStringToByteArray("06 00 0000000A 00 00 00 00");
           log = "DICOMSCU >> A - RELEASE - RP PDU" + "\r\n";
            return pdu1;
        }
        public override string Log()
        {
            return ("DICOMSCU << A - RELEASE - RQ PDU" + "\r\n");

        }
        //分割PDU并存入相应字段
        public override void PDUSplit(byte[] pdu)
        {
            Result = pdu[1];
            Source = pdu[2];
            Reason = pdu[3];
        }
    }
    //AReleaseRP
    public class AReleaseRP : PDU
    {
        public override PDU AETitle(PDU pdu1)
        {
                       return pdu1;
        }
        public override string Log()
        {
            return ("DICOMSCU >> A-RELEASE-RP PDU" + "\r\n");

        }
        //分割PDU并存入相应字段
        public override void PDUSplit(byte[] pdu)
        {
            Result = pdu[1];
            Source = pdu[2];
            Reason = pdu[3];
        }
    }

}

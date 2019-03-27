using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;

/********************************* 패킷
 * CONNECT : 접속 성공
 * DISCONNECT : 접속 끊김
 * LOGIN : 로그인
 * LOGOUT : 로그아웃
 * USER : 유저 정보 ( 내가 아닌 유저 )
 * ADDUSER : 나 ( 본인 추가 )
 */

namespace Server
{
    class User
    {
        UserData userData = new UserData();
        /**** 유저가 가지고 있을 정보 (변수) ********************/
        string nickName = "";
        int roomIdx = 0;       // 0은 로비 그 외는 방 번호
        /****************************************************/
        
        public User(Socket socket)
        {
            userData.workSocket = socket;

            userData.workSocket.BeginReceive(userData.buf, userData.recvLen, UserData.BUFFER_SIZE, 0, new AsyncCallback(ReadCallBack), userData);
            SendMsg("CONNECT");
        }

        /**
         * @brief 클라이언트로 보내는 패킷
         * @param result 결과
         */
        void ReadCallBack(IAsyncResult result)
        {
            try
            {
                Socket handler = userData.workSocket;
                int bytesRead = handler.EndReceive(result);

                if (bytesRead > 0)
                {
                    userData.recvLen += bytesRead;

                    while (true)
                    {
                        short len = 0;
                        Util.GetShort(userData.buf, 0, out len);

                        if (len > 0 && userData.recvLen >= len)
                        {
                            ParsePacket(len);
                            userData.recvLen -= len;

                            if (userData.recvLen > 0)
                            {
                                Buffer.BlockCopy(userData.buf, len, userData.buf, 0, userData.recvLen);
                            }
                            else
                            {
                                handler.BeginReceive(userData.buf, userData.recvLen, UserData.BUFFER_SIZE, 0, new AsyncCallback(ReadCallBack), userData);
                                break;
                            }
                        }
                        else
                        {
                            handler.BeginReceive(userData.buf, userData.recvLen, UserData.BUFFER_SIZE, 0, new AsyncCallback(ReadCallBack), userData);
                            break;
                        }
                    }
                }
                else
                {
                    handler.BeginReceive(userData.buf, userData.recvLen, UserData.BUFFER_SIZE, 0, new AsyncCallback(ReadCallBack), userData);
                }
            }
            catch (Exception)
            {
                Server.RemoveUser(this);
                Console.WriteLine("User Closed");
            }
        }

        /**
         * brief 패킷 분석
         * param len 길이
         */
        private void ParsePacket(int len)
        {
            string msg = Encoding.UTF8.GetString(userData.buf, 2, len - 2);
            string[] txt = msg.Split(':');      // 암호를 ':' 로 분리해서 읽음

            /************* 기능이 추가되면 덧붙일 것 ***************/
            if (txt[0].Equals("LOGIN"))
            {
                roomIdx = 0;
                nickName = txt[1];
                //Login();
                Console.WriteLine(txt[1] + " is Login.");
            }
            else if (txt[0].Equals("DISCONNECT"))
            {
                if (nickName.Length > 0)
                {
                    Console.WriteLine(nickName + " is Logout.");
                    Logout();
                }
                userData.workSocket.Shutdown(SocketShutdown.Both);
                userData.workSocket.Close();
            }
            else if (txt[0].Equals("FOUND_ROOM"))
            {
                for (int j = 0; j < Server.v_rooms.Count; j++)
                    if (Server.v_rooms[j].nowUser < Server.v_rooms[j].limitUser)
                        SendMsg(string.Format("FOUND_ROOM:{0}:{1}:{2}:{3}:{4}", Server.v_rooms[j].roomIdx, Server.v_rooms[j].roomName, Server.v_rooms[j].roomPW, Server.v_rooms[j].nowUser, Server.v_rooms[j].limitUser));
            }
            else if (txt[0].Equals("FAST_ROOM"))
            {
                for (int j = 0; j < Server.v_rooms.Count; j++)
                    if (Server.v_rooms[j].nowUser < Server.v_rooms[j].limitUser && Server.v_rooms[j].roomPW.Equals(""))
                    {
                        IntoRoom(Server.v_rooms[j].roomIdx.ToString());
                        break;
                    }
                
            }
            else if (txt[0].Equals("ROOM_EXIT"))
            {
                byte totalMem = 0;
                String[] roomUserNames = { "_", "_" };
                int tempCount = 0;
                for (int k = 0; k < Server.v_user.Count; k++)
                {
                    if (Server.v_user[k].roomIdx.Equals(roomIdx) && Server.v_user[k] != this)
                    {
                        roomUserNames[tempCount] = Server.v_user[k].nickName;
                        tempCount++;
                    }
                    // 인원수는 총 3명인데 2명을 찾았다는 것은 방에 들어가는 모든 인원을 찾았다는 것
                    if (tempCount >= 2)
                        break;
                }
                tempCount = 0;
                for (int i = 0; i < Server.v_user.Count; i++)
                {
                    if (Server.v_user[i].roomIdx.Equals(roomIdx) && Server.v_user[i] != this)
                    {
                        Console.WriteLine(string.Format("SOMEONE_EXIT:{0}:{1}", roomUserNames[0], roomUserNames[1]));
                        if (Server.v_user[i].nickName.Equals(roomUserNames[0]))
                            Server.v_user[i].SendMsg(string.Format("SOMEONE_EXIT:{0}", roomUserNames[1]));
                        else
                            Server.v_user[i].SendMsg(string.Format("SOMEONE_EXIT:{0}", roomUserNames[0]));
                        tempCount++;
                        totalMem++;
                    }
                    if (tempCount >= 2)
                        break;
                }

                for (int i = 0; i < Server.v_rooms.Count; i++)
                    if (Server.v_rooms[i].roomIdx.Equals(roomIdx))
                    {
                        if (totalMem.Equals(0))
                            Server.v_rooms.RemoveAt(i) ;
                        else
                            Server.v_rooms[i].nowUser = totalMem;
                    }

                roomIdx = 0;

                Console.WriteLine("----REFRESH-------------------------");
                for (int i = 0; i < Server.v_user.Count; i++)
                {
                    Console.WriteLine(string.Format("name : {0} , roomIdx : {1}", Server.v_user[i].nickName, Server.v_user[i].roomIdx));
                }
                Console.WriteLine("------------------------------------");
            }
            else if (txt[0].Equals("INTO_ROOM"))
            {
                IntoRoom(txt[1]);
            }
            else if (txt[0].Equals("GAME_START"))
            {
                Console.WriteLine("GAME START : " + roomIdx);
                for (int i = 0; i < Server.v_user.Count; i++)
                {
                    if (Server.v_user[i].roomIdx.Equals(roomIdx))
                    {
                        Server.v_user[i].SendMsg("GAME_START");
                        Console.WriteLine("SEND GAME START : " + Server.v_user[i].nickName);
                    }
                }
            }
            else if (txt[0].Equals("CREATE_ROOM"))
            {
                //Logout();
                CreateRoom(txt[1], txt[2], txt[3]);
                SendMsg(string.Format("CHANGE_ROOM:{0}", roomIdx));
            }
            else if (txt[0].Equals("OUT_ROOM"))
            {
                for (int i = 0; i < Server.v_rooms.Count; i++)
                {
                    if (Server.v_rooms[i].roomIdx.Equals(int.Parse(txt[1])))
                    {
                        roomIdx = 0;
                        Server.v_rooms[i].nowUser--;
                        if (Server.v_rooms[i].nowUser <= 0)
                            Server.v_rooms.RemoveAt(i);
                        break;
                    }
                }
            }
            else
            {
                //!< 이 부분에 들어오는 일이 있으면 안됨 (패킷 실수)
                Console.WriteLine("Un Correct Message ");
            }
        }


        /**
         * @brief 유저가 나가졌을때 다른 유저에게 이를 알림
         */
        void Logout()
        {
            // 로비가 아닌 경우에만 알림 (로비에는 접속|로그아웃 패킷이 필요없기 때문)
            if (roomIdx > 0)
            {
                for (int i = 0; i < Server.v_user.Count; i++)
                {
                    if (Server.v_user[i] != this)
                    {
                        Server.v_user[i].SendMsg(string.Format("LOGOUT:{0}", roomIdx));
                    }
                }
            }
        }

        /**
         * @brief 채팅
         */
        void Chat(string txt)
        {
            int idx = Server.v_user.IndexOf(this);

            for (int i = 0; i < Server.v_user.Count; i++)
            {
                Server.v_user[i].SendMsg(string.Format("CHAT:{0}:{1}", idx, txt));
            }
        }

        /**
         * @brief 이동
         */
        void Move()
        {
        }

        /**
         * @brief 방 생성
         */
        void CreateRoom(string roomName, string roomPW, string limitMem)
        {
            INFO.Room room = new INFO.Room();
            room.roomIdx = ++Server.roomCount;
            room.roomName = roomName;
            room.roomPW = roomPW;
            room.limitUser = byte.Parse(limitMem);
            room.nowUser = 1;

            roomIdx = room.roomIdx;

            Server.v_rooms.Add(room);
            Console.WriteLine(string.Format("CREATE ROOM ({0}) : {1} : {2}", room.roomIdx, room.roomName, room.roomPW));
        }

        /**
         * @brief 방 입장
         */
        void IntoRoom(string roomIdx)
        {
            Console.WriteLine("----START---------------------------");
            for (int i = 0; i < Server.v_user.Count; i++)
            {
                Console.WriteLine(string.Format("name : {0} , roomIdx : {1}", Server.v_user[i].nickName, Server.v_user[i].roomIdx));
            }
            Console.WriteLine("------------------------------------");

            bool isExistence = false;     // 도중에 방이 사라졌는지 체크

            for (int i = 0; i < Server.v_rooms.Count; i++)
            {
                if (Server.v_rooms[i].roomIdx.Equals(int.Parse(roomIdx)))
                {
                    isExistence = true;
                    // 내가 들어와도 인원한계치를 넘어가지 않는지를 체크
                    if (Server.v_rooms[i].nowUser + 1 <= Server.v_rooms[i].limitUser)
                    {
                        Server.v_rooms[i].nowUser++;

                        String[] roomUserNames = { "_", "_" };
                        int tempCount = 0;
                        for (int k = 0; k < Server.v_user.Count; k++)
                        {
                            Console.WriteLine(string.Format("ROOM IDX : {0}", roomIdx));
                            // 같은 방에 있는 모든 다른 유저에게 접속함을 알림
                            if (Server.v_user[k].roomIdx.Equals(int.Parse(roomIdx)) && Server.v_user[k] != this)
                            {
                                Server.v_user[k].SendMsg(string.Format("SOMEONE_ENTER:{0}", nickName));
                                roomUserNames[tempCount] = Server.v_user[k].nickName;
                                tempCount++;
                                Console.WriteLine(string.Format("Hi {0}, I am {1}. Im Entered", Server.v_user[k].nickName, nickName));
                            }
                            // 인원수는 총 3명인데 2명을 찾았다는 것은 방에 들어가는 모든 인원을 찾았다는 것
                            if (tempCount >= 2)
                                break;
                        }

                        Console.WriteLine(string.Format("BEFORE USER INFO :{0}:{1}", roomUserNames[0], roomUserNames[1]));
                        SendMsg(string.Format("ENTER_ROOM:IN:{0}:{1}", roomUserNames[0], roomUserNames[1]));       // 방에 들어가도 됨을 허락 받음 : 방에 들어와 있는 유저들 정보
                        this.roomIdx = int.Parse(roomIdx);
                    }
                    else
                    {
                        SendMsg("ENTER_ROOM:LIMIT");    // 방 인원수가 꽉 참
                    }
                    break;
                }
            }

            if (!isExistence)
                SendMsg("ENTER_ROOM:MISS");     // 도중에 방이 사라짐

            Console.WriteLine("----END-----------------------------");
            for (int i = 0; i < Server.v_user.Count; i++)
            {
                Console.WriteLine(string.Format("name : {0} , roomIdx : {1}", Server.v_user[i].nickName, Server.v_user[i].roomIdx));
            }
            Console.WriteLine("------------------------------------");
        }

        /**
         * @brief 클라이언트로 보내는 패킷
         * @param msg 클라이언트가 인식할 메세지, 일종의 암호 (?)
         */
        void SendMsg(string msg)
        {
            try
            {
                if (userData.workSocket != null && userData.workSocket.Connected)
                {
                    byte[] buff = new byte[4096];
                    Buffer.BlockCopy(ShortToByte(Encoding.UTF8.GetBytes(msg).Length + 2), 0, buff, 0, 2);
                    Buffer.BlockCopy(Encoding.UTF8.GetBytes(msg), 0, buff, 2, Encoding.UTF8.GetBytes(msg).Length);
                    userData.workSocket.Send(buff, Encoding.UTF8.GetBytes(msg).Length + 2, 0);
                }
            }
            catch (System.Exception e)
            {
                if (nickName.Length > 0) Logout();

                userData.workSocket.Shutdown(SocketShutdown.Both);
                userData.workSocket.Close();

                Server.RemoveUser(this);

                Console.WriteLine("SendMsg Error : " + e.Message);
            }
        }

        /**
         * @brief 클라이언트로 보내는 패킷
         */
        byte[] ShortToByte(int val)
        {
            byte[] temp = new byte[2];
            temp[1] = (byte)((val & 0x0000ff00) >> 8);
            temp[0] = (byte)((val & 0x000000ff));
            return temp;
        }
    }
}

﻿/*
    Copyright 2015 MCGalaxy
        
    Dual-licensed under the Educational Community License, Version 2.0 and
    the GNU General Public License, Version 3 (the "Licenses"); you may
    not use this file except in compliance with the Licenses. You may
    obtain a copy of the Licenses at
    
    http://www.opensource.org/licenses/ecl2.php
    http://www.gnu.org/licenses/gpl-3.0.html
    
    Unless required by applicable law or agreed to in writing,
    software distributed under the Licenses are distributed on an "AS IS"
    BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express
    or implied. See the Licenses for the specific language governing
    permissions and limitations under the Licenses.
 */
using System;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using MCGalaxy.Config;
using MCGalaxy.Network;
using MCGalaxy.Tasks;

namespace MCGalaxy.Modules.Discord {
    
    public sealed class DiscordWebsocket : ClientWebSocket {
        public string Token;
        public Action<JsonObject> Handler;
        public Func<string> GetStatus;
        string lastSequence;
        TcpClient client;
        SslStream stream;
        
        const int OPCODE_DISPATCH        = 0;
        const int OPCODE_HEARTBEAT       = 1;
        const int OPCODE_IDENTIFY        = 2;
        const int OPCODE_STATUS_UPDATE   = 3;
        const int OPCODE_VOICE_STATE_UPDATE = 4;
        const int OPCODE_RESUME          = 6;
        const int OPCODE_REQUEST_SERVER_MEMBERS = 8;
        const int OPCODE_INVALID_SESSION = 9;
        const int OPCODE_HELLO           = 10;
        const int OPCODE_HEARTBEAT_ACK   = 11;
        
        
        public DiscordWebsocket() {
            path = "/?v=6&encoding=json";
        }
        
        const string host = "gateway.discord.gg";
        // stubs
        public override bool LowLatency { set { } }
        public override string IP { get { return ""; } }
        
        public void Connect() {
            client = new TcpClient();
            client.Connect(host, 443);

            stream   = HttpUtil.WrapSSLStream(client.GetStream(), host);
            protocol = this;
            Init();
        }
        
        protected override void WriteCustomHeaders() {
            WriteHeader("Authorization: Bot " + Token);
            WriteHeader("Host: " + host);
        }
        
        public override void Close() { client.Close(); }
        
        protected override void Disconnect(int reason) {
            base.Disconnect(reason);
            Close();
        }
        
        
        public void ReadLoop() {
            byte[] data = new byte[4096];
            for (;;) {
                int len = stream.Read(data, 0, 4096);
                if (len == 0) break; // disconnected
                HandleReceived(data, len);
            }
        }
        
        protected override void HandleData(byte[] data, int len) {
            string value   = Encoding.UTF8.GetString(data, 0, len);
            JsonReader ctx = new JsonReader(value);
            JsonObject obj = (JsonObject)ctx.Parse();
            
            Logger.Log(LogType.SystemActivity, value);
            if (obj == null) return;
            
            int opcode = int.Parse((string)obj["op"]);
            DispatchPacket(opcode, obj);
        }
        
        void DispatchPacket(int opcode, JsonObject obj) {
            if (opcode == OPCODE_DISPATCH) HandleDispatch(obj);
            if (opcode == OPCODE_HELLO)    HandleHello(obj);
        }
        
        
        void HandleHello(JsonObject obj) {
            JsonObject data = (JsonObject)obj["d"];
            string interval = (string)data["heartbeat_interval"];            
            int msInterval  = int.Parse(interval);
            
            Server.Background.QueueRepeat(SendHeartbeat, null, 
                                          TimeSpan.FromMilliseconds(msInterval));
            SendIdentify();
        }
        
        void HandleDispatch(JsonObject obj) {
            // update last sequence number
            object sequence;
            if (obj.TryGetValue("s", out sequence)) 
                lastSequence = (string)sequence;
            
            Handler(obj);
        }
        
        void HandleMessageEvent(JsonObject obj) {
            JsonObject data   = (JsonObject)obj["d"];
            JsonObject author = (JsonObject)data["author"];
            string message    = (string)data["content"];
            
            string user = (string)author["username"];
            string msg  = "&I(Discord) " + user + ": &f" + message;
            Logger.Log(LogType.IRCChat, msg);
            Chat.Message(ChatScope.Global, msg, null, null);
        }
        
        
        public void SendMessage(int opcode, JsonObject data) {
            JsonObject obj = new JsonObject();
            obj["op"] = opcode;
            obj["d"]  = data;
            SendMessage(obj);
        }
        
        public void SendMessage(JsonObject obj) {
            StringWriter dst  = new StringWriter();
            JsonWriter   w    = new JsonWriter(dst);
            w.SerialiseObject = raw => JsonSerialisers.WriteObject(w, raw);
            w.WriteObject(obj);
            
            string str = dst.ToString();
            Send(Encoding.UTF8.GetBytes(str), SendFlags.None);
        }
        
        protected override void SendRaw(byte[] data, SendFlags flags) {
            stream.Write(data);
        }
        
        void SendHeartbeat(SchedulerTask task) {
            JsonObject obj = new JsonObject();
            obj["op"] = OPCODE_HEARTBEAT;
            
            if (lastSequence != null) {
                obj["d"] = int.Parse(lastSequence);
            } else {
                obj["d"] = null;
            }
            SendMessage(obj);
        }
        
        const int INTENT_GUILD_MESSAGES = 1 << 9;
        
        public void SendIdentify() {
            JsonObject data = new JsonObject();
            
            JsonObject props = new JsonObject();
            props["$os"] = "linux";
            props["$browser"] = "MCGRelayBot";
            props["$device"]  = "MCGRelayBot";
            
            data["token"]   = Token;
            data["intents"] = INTENT_GUILD_MESSAGES;
            data["properties"] = props;
            data["presence"]   = MakePresence();
            SendMessage(OPCODE_IDENTIFY, data);
        }
        
        public void SendUpdateStatus() {
        	JsonObject data = MakePresence();
        	SendMessage(OPCODE_STATUS_UPDATE, data);
        }
        
        JsonObject MakePresence() {
            JsonObject activity = new JsonObject();
            activity["name"]    = GetStatus();
            activity["type"]    = 0;
            
            JsonArray activites = new JsonArray();
            activites.Add(activity);
            
            JsonObject obj = new JsonObject();
            obj["activities"] = activites;
            obj["status"]     = "online";
            obj["afk"]        = false;
            return obj;
        }
    }
}

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
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using MCGalaxy.Config;
using MCGalaxy.Network;

namespace MCGalaxy.Modules.Relay.Discord {
    
    /// <summary> Represents an abstract Discord API message </summary>
    public abstract class DiscordApiMessage {
        /// <summary> The path/route that will handle this message </summary>
        public string Path;
        
        /// <summary> Converts this message into its JSON representation </summary>
        public abstract JsonObject ToJson();
    }
    
    /// <summary> Message for sending text to a channel </summary>
    public class ChannelSendMessage : DiscordApiMessage {
        public string Content;
        
        public string CalcPath(string channelID) { return "/channels/" + channelID + "/messages"; }
        
        public override JsonObject ToJson() {
            // no pinging everyone
            JsonObject allowed = new JsonObject()
            {
                { "parse", new JsonArray() { "users", "roles" } }
            };
            JsonObject obj = new JsonObject()
            {
                { "content", Content },
                { "allowed_mentions", allowed }
            };
            return obj;
        }
    }
    
    public class ChannelSendEmbed : ChannelSendMessage {

        public override JsonObject ToJson() {
            JsonObject embed = new JsonObject()
            {
                { "description", Content },
            };
            JsonObject obj = base.ToJson();
            obj.Remove("content");
            
            obj["embed"] = embed;
            return obj;
        }
    }
    
    /// <summary> Implements a basic web client for communicating with Discord's API </summary>
    /// <remarks> https://discord.com/developers/docs/reference </remarks>
    /// <remarks> https://discord.com/developers/docs/resources/channel#create-message </remarks>
    public sealed class DiscordApiClient {
        public string Token;
        const string host = "https://discord.com/api";
        AutoResetEvent handle = new AutoResetEvent(false);
        volatile bool terminating;
        
        Queue<DiscordApiMessage> requests = new Queue<DiscordApiMessage>();
        readonly object reqLock = new object();
            
        
        void HandleNext() {
            DiscordApiMessage msg = null;
            lock (reqLock) {
                if (requests.Count > 0) msg = requests.Dequeue();
            }
            if (msg == null) { handle.WaitOne(); return; }
            
            // TODO HttpWebRequest
            using (WebClient client = HttpUtil.CreateWebClient()) {
                client.Headers[HttpRequestHeader.ContentType]   = "application/json";
                client.Headers[HttpRequestHeader.Authorization] = "Bot " + Token;

                string data = Json.SerialiseObject(msg.ToJson());
                string resp = client.UploadString(host + msg.Path, data);
                Logger.Log(LogType.SystemActivity, resp);
            }
        }
        
        void SendLoop() {
            for (;;) {
                if (terminating) break;
                
                try {
                    HandleNext();
                } catch (Exception ex) {
                    Logger.LogError(ex);
                }
            }
            
            // cleanup state
            try { 
                lock (reqLock) requests.Clear();
                handle.Dispose(); 
            } catch {
            }
        }
        
        
        void WakeupWorker() {
            try {
                handle.Set();
            } catch (ObjectDisposedException) {
                // for very rare case where handle's already been destroyed
            }
        }
        
        public void RunAsync() {
            Thread worker = new Thread(SendLoop);
            worker.Name   = "Discord-ApiClient";
            worker.IsBackground = true;
            worker.Start();
        }
        
        public void StopAsync() {
            terminating = true;
            WakeupWorker();
        }       
        
        public void SendAsync(DiscordApiMessage msg) {
            lock (reqLock) requests.Enqueue(msg);
            WakeupWorker();
        }
        
        public void SendMessageAsync(string channelID, string message) {
            ChannelSendMessage msg = new ChannelSendMessage();
            msg.Path    = msg.CalcPath(channelID);
            msg.Content = message;
            SendAsync(msg);
        }
    }
}

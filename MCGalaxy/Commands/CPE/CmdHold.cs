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
using MCGalaxy.Network;
using BlockID = System.UInt16;

namespace MCGalaxy.Commands.CPE 
{    
    public sealed class CmdHold : Command2 
    {
        public override string name { get { return "Hold"; } }
        public override string shortcut { get { return "HoldThis"; } }
        public override string type { get { return CommandTypes.Building; } }
        public override LevelPermission defaultRank { get { return LevelPermission.AdvBuilder; } }
        public override bool SuperUseable { get { return false; } }

        public override void Use(Player p, string message, CommandData data) {
            if (message.Length == 0) { Help(p); return; }      
            string[] args = message.SplitSpaces(3);
            
            CpeMessageType type = 0;
            if (!CommandParser.GetEnum(p, args[0], "Type", ref type)) return;
            
            PersistentMessagePriority pri = 0;
            if (!CommandParser.GetEnum(p, args[1], "PRI", ref pri)) return;
            
            string msg = args.Length > 2 ? args[2] : "";
            p.SendCpeMessage(type, msg, pri);
            p.Message("----");
        }
        
        public override void Help(Player p) {
            p.Message("&T/Hold [block] <locked>");
            p.Message("&HMakes you hold the given block in your hand");
            p.Message("&H  <locked> optionally prevents you from changing it");
        }
    }
}

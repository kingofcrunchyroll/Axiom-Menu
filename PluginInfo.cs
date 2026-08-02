/*
 * Seralyth Menu  PluginInfo.cs
 * A community driven mod menu for Gorilla Tag with over 1000+ mods
 *
 * Copyright (C) 2026  Seralyth Software
 * https://github.com/Seralyth/Seralyth-Menu
 * 
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

namespace Seralyth
{
    public class PluginInfo
    {
        public const string GUID = "org.fluxed.gorillatag.axiommenu";
        public const string Name = "Axiom Menu";
        public const string Description = "The 'More Legal' Seralyth, A Mod Menu built to Entertain.";
        public const string BuildTimestamp = "2026-07-08T15:58:08Z";
        public const string Version = "1.5.8";

        public const string BaseDirectory =
#if LEGAL || LEGAL_DEBUG
            "SeralythMenu/Legal";
#else
            "AxiomMenu";
#endif
        public const string ClientResourcePath = "AxiomMenu.Resources.Client";
        public const string ServerResourcePath = "https://raw.githubusercontent.com/Seralyth/Seralyth-Menu/master/Resources/Server";
        public const string AxiomServerPath = "https://raw.githubusercontent.com/FluxedGaming-git/Axiom-Server/refs/heads/main/";
        public const string ServerAPI = "https://menu.seralyth.software";
        public const string Logo = @"
                                          
                     .                    
                   :##=                   
                   @@%@%                  
                   +#%@                   
                    #%%                   
                    +@=                   
                    *@+                   
                 ** %@*#*:                
             :+%@@@:%@*####+%             
           .%%%#@   #@+  :##@@#           
           :@%*     @@=    *%@=           
            @%+     @%+    =@@:           
            @@* :+@@@**#*. +@@:           
            +*#%@@@=  =@@@@%+@            
          :*%@@@@        @%@@@@-          
     * =*###*=:#@@@#   @@@#+=*###*@ @:    
    @@@@@@-     :*#@@@@@%-     :*#@@@%=   
   **++%           :++*           =++*+.  
                                          
";

#if DEBUG || LEGAL_DEBUG
        public static bool BetaBuild = true;
#else
        public static bool BetaBuild = false;
#endif
    }
}

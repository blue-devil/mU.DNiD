﻿/*
    DNiD 2 - PE Identifier.
    Copyright (C) 2016  mammon

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DNiD2.intClasses
{
    static class clsScanner
    {
        /// <summary>
        /// Sets the signature DataBase to read the signatures from...
        /// </summary>
        /// <param name="useInternalOnly">If internal DB...</param>
        /// <param name="useExternalOnly">If external DB...</param>
        /// <param name="useBothIntExt">If both internal and external DB...</param>
        /// <param name="externalDB">[Optional] Filepath to external DB...</param>
        public static void SetSignatureDB(bool useInternalOnly, bool useExternalOnly, bool useBothIntExt, [Optional]string externalDB)
        {
            Signatures.Initialize();

            if (useInternalOnly)
                mySigs = Signatures.SignatureList;
            if (useExternalOnly && File.Exists(externalDB))
                mySigs = CreateExt(false, File.ReadAllLines(externalDB));
            if (useBothIntExt && File.Exists(externalDB))
                mySigs = CreateExt(true, File.ReadAllLines(externalDB));
        }
        /// <summary>
        /// Basically a primitive INI-file parser...
        /// </summary>
        /// <param name="useIntToo"></param>
        /// <param name="sigListX"></param>
        /// <returns></returns>
        private static Dictionary<string, Tuple<short[], bool>> CreateExt(bool useIntToo, string[] sigListX)
        {
            var sigList = new Dictionary<string, Tuple<short[], bool>>();

            var i = 0;
            var y = " ";
            while (i < sigListX.Length)
            {
                if (sigListX[i].StartsWith("[") && sigListX[i].EndsWith("]") && sigListX[i + 1].Contains("=") && sigListX[i + 2].Contains("="))
                {
                    var sigInfo = sigListX[i].Substring(1, sigListX[i].Length - 2);
                    var signature = sigListX[i + 1].GetKeyValue().RemoveWhiteSpaces().HexToShortArray();
                    var sigUseEpOnly = sigListX[i + 2].GetKeyValue().StringToBoolean();

                    if (sigList.ContainsKey(sigInfo) && !sigList.ContainsValue(Tuple.Create(signature, sigUseEpOnly)))
                    {
                        sigList.Add(sigInfo + y, Tuple.Create(signature, sigUseEpOnly));
                    }
                    if (!sigList.ContainsKey(sigInfo))
                    {
                        sigList.Add(sigInfo, Tuple.Create(signature, sigUseEpOnly));
                    }
                }
                i++;
            }

            if (!useIntToo)
                return sigList;
            else
            {
                foreach (var yx in Signatures.SignatureList)
                {
                    if (!sigList.Contains(yx))
                    {
                        sigList.Add(yx.Key, yx.Value);
                    }
                }

                return sigList;
            }
        }
        private static Dictionary<string, Tuple<short[], bool>> mySigs;

        private static bool SearchBytes(byte[] file, short[] signature, long startOffset)
        {
            var wildCards = new bool[signature.Length];
            for (var i = 0; i < signature.Length; i++)
            {
                if (signature[i] == -1)
                {
                    wildCards[i] = true;
                }
                else
                {
                    wildCards[i] = false;
                }
            }
            return FindPattern(file, signature, wildCards, (int)startOffset);
        }
        private static bool FindPattern(byte[] Body, short[] Pattern, bool[] Wild, int start = 0)
        {
            var foundIndex = -1;
            var match = false;

            if (Body.Length > 0
                && Pattern.Length > 0
                && start <= Body.Length - Pattern.Length && Pattern.Length <= Body.Length)
            {
                for (int index = start; index <= Body.Length - Pattern.Length; index++)
                {
                    if (Wild[0] || (Body[index] == Pattern[0]))
                    {
                        match = true;
                        for (int index2 = 1; index2 <= Pattern.Length - 1; index2++)
                        {
                            if (!Wild[index2] &&
                              (Body[index + index2] != Pattern[index2]))
                            {
                                match = false;
                                break;
                            }
                        }

                        if (match)
                        {
                            foundIndex = index;
                            break;
                        }
                    }
                }
            }
            return foundIndex > -1 ? true : false;
        }
        /// <summary>
        /// Scan through an array of bytes (the entire file that you want to scan)...
        /// </summary>
        /// <param name="lpBuffer">The file array to scan...</param>
        /// <returns>Protector detected</returns>
        public static string Scan(byte[] lpBuffer)
        {
            var toReturn = "Unknown or no protection!";
            Parallel.ForEach(mySigs, (pair) =>
            {
                if (pair.Value.Item2)
                {
                    using (var a = new dnlib.PE.PEImage(lpBuffer))
                    {
                        var b = (uint)a.ToFileOffset(a.ImageNTHeaders.OptionalHeader.AddressOfEntryPoint);
                        if (SearchBytes(lpBuffer, pair.Value.Item1, b))
                        {
                            toReturn = pair.Key;
                            return;
                        }
                    }
                }
                else
                {
                    if (SearchBytes(lpBuffer, pair.Value.Item1, 0L))
                    {
                        toReturn = pair.Key;
                        return;
                    }
                }
            });
            return toReturn;
        }
        /// <summary>
        /// Scan through a file
        /// </summary>
        /// <param name="filePath">Path of the file to scan...</param>
        /// <returns>Protector detected</returns>
        public static string Scan(string filePath)
        {
            return Scan(File.ReadAllBytes(filePath));
        }
    }
}
/** KentDisplayProtocol.cs

   C# implementation of the protocol and interface to program Kent Displays Protocol 25016

   Author: Armin Costa
   e-mail: armincosta@hotmail.com
   
#   
# Copyright (C) 2005, 2013 Armin Costa
# This file is part of Kent_display_serial_protocol_25016
#
# Kent_display_serial_protocol_25016 is free software: you can redistribute it and/or modify
# it under the terms of the GNU General Public License as published by
# the Free Software Foundation, either version 3 of the License, or
# (at your option) any later version.
#
# Kent_display_serial_protocol_25016 is distributed in the hope that it will be useful,
# but WITHOUT ANY WARRANTY; without even the implied warranty of
# MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
# GNU General Public License for more details.
#
# You should have received a copy of the GNU General Public License version 3
*/
using System;
using System.IO;
using System.Data;
using System.Runtime.InteropServices;
using Serial;
using System.Drawing;


namespace SerialTest 
{
	public class KentDisplayProtocol
	{
		protected static int  MAX_PACKET_SIZE = 10024;
		public byte[] RX_BUFFER = new byte[MAX_PACKET_SIZE];
		public byte[] TX_BUFFER;
		/** the following codes are used when master-device wake-up operations are performed
		using RF-communication, in this case you may have to consider some
		time-delays (1-2 sec. -- (n)*0x55). If using a serial communication link, those time-delays decrease
			 */
		protected byte WAKE_UP = 0x55;      // last 1 sec. if RF is used
		protected byte ETX = 0x03;  // End of TX cmd
		static DataPacket pPacket = null;
		Port portOpened;

		public KentDisplayProtocol(ref Port pPort)
		{
			portOpened = pPort;
		}

		~KentDisplayProtocol()
		{
			pPacket = null;
		}

		public byte[] getMemoryManagementFragment()
		{
			return new byte[]{0x00, 0x00, 0x1B, 0x01, 0x02, 0x3E, 0x5B, 0x0D, 0x0A, 0x55, 0x00};

		}

		public byte[] CreatePacket(int type, int nr, byte[] data)
		{
			pPacket = new DataPacket(type);
			pPacket.PACKET_NR = nr;
			if(data != null)
			{
				return pPacket.compile(data);
			}
			else
			{
				return pPacket.compile(null);
			}		
		}


		public byte[] ReverseArr(byte[] b_arr)
		{
			for(int i = 0; i < b_arr.Length; i++)
			{
				b_arr[i] = Reverse(b_arr[i]);
			}
			return b_arr;
		}

		public  byte Reverse(byte inByte)
		{
			byte result = 0x00;
			byte mask = 0x00;
			for ( mask = 0x80;
				Convert.ToInt32(mask) > 0; 
				mask >>= 1)
			{
				result >>= 1;
				byte tempbyte = (byte)(inByte & mask) ;
				if (tempbyte != 0x00)
					result |= 0x80;
			}
			return (result);
		}

		public byte[] ReadFromFile(string filename, int max_read)
		{
			StreamReader SR;
			FileStream f_s;
                        try{
      			    f_s = File.Open(filename, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read);
      			    byte[] b_buff = new byte[MAX_PACKET_SIZE];
      			    byte[] output_b2;
      			    int read, cnt;
      			    read = f_s.Read(b_buff, 0, MAX_PACKET_SIZE);
      			    if(max_read != 0)
      			    {
      				read = max_read;
      			    }
      			    output_b2 = new byte[read];
      			    for(cnt = 0; cnt < read; cnt++)
      			    {
      				output_b2[cnt] = b_buff[cnt];
      			    }
      			    f_s.Close();
      			    return output_b2;
                      }catch(Exception e){
                          return new byte[] {0x45, 0x52, 0x52, 0x4F, 0x52};
                      }
		}

		public byte[] sendPacket(byte[] buf)
		{
			int x,  nop_count = 100;
			byte[] b = new byte[1];
			b[0] = WAKE_UP;
			byte[] input_b;
			byte[] bufferT;
			byte[] begin;
			byte[] end;
			TX_BUFFER = buf;
			wakeUP(30);
			begin = getMemoryManagementFragment();
			bufferT = new byte[TX_BUFFER.Length];
			Array.Copy(TX_BUFFER, 0, bufferT, 0, TX_BUFFER.Length);
			byte[] a_b = new byte[1];
			for(int i = 0; i < bufferT.Length; i++)
			{
				a_b[0] = bufferT[i];
				portOpened.Output = a_b;
				
			}
			wakeUP(30);
			if(TX_BUFFER != null)
			{
				return bufferT;
			}
			else
			{
				return new byte[] {0x45, 0x52, 0x52, 0x4F, 0x52}; 
			}
		}

		void wakeUP(int wake_count)
		{
			int x;
			byte[] b  = new byte[1];
			for(x = 0; x < wake_count; x++)
			{
			        b[0] = (byte)WAKE_UP;
        			portOpened.Output = b;
			}
		}

		public byte[] receivePacket()
		{
			DataResponse res;
			RX_BUFFER = (byte[])portOpened.Input;
			if(pPacket == null)
			{
				res = new DataPacket(1);
			}
			else
			{
				res = (DataResponse)pPacket;
			}
			if(res.checkResponseMsg(RX_BUFFER))
			{
				return RX_BUFFER;
			}
			else
			{
				return new byte[] {0x45, 0x52, 0x52, 0x4F, 0x52};
			}
		}
                 
		public byte[] executeMsg()
		{
			DataPacket execPacket = new DataPacket(17);
			portOpened.Output = execPacket.compile(null);

			RX_BUFFER = (byte[])portOpened.Input;
			DataResponse res = (DataResponse)execPacket;
			if(res.checkResponseMsg(RX_BUFFER))
			{
				return RX_BUFFER;
			}
			else
			{
				return new byte[] {0x45, 0x52, 0x52, 0x4F, 0x52}; 
			}
		}
	}
	
	/* contains Fields #4-6
	 */
	internal class DataPacket : DataSkelekton 
	{
		private byte[] TMP;
		private byte[] BUFFER;
		private int buffer_size;

		private byte[] CMD_DES_1 = new byte[1];
		private byte[] CMD_DES_2;
		private byte[] DATA;

		public int PACKET_NR = 0;

		public DataPacket(int type)
		{
			this.TYPE = type;
		}

		public byte[] compile(byte[] binary_content)
		{
			bool execCMD = false;
			DATA = null;
			switch(TYPE)
			{
				case 0:
					CMD_DES_1[0] = (byte)ProtocolCmd.LOAD_TEXT;
					CMD_DES_2 = new byte[]{(byte)PacketBaseConfig.master_address, (byte)PacketBaseConfig.packet_nr_1, (byte)PacketBaseConfig.sense};
					break;    
				case 1:
					CMD_DES_1[0] = (byte)ProtocolCmd.LOAD_BITMAP;
					CMD_DES_2 = new byte[]{(byte)PacketBaseConfig.master_address, (byte)this.PACKET_NR, (byte)PacketBaseConfig.sense};
					break;
				case 2:
					break;
				case 3:
					CMD_DES_1[0] = (byte)ProtocolCmd.CMD_3;
					CMD_DES_2 = new byte[]{0x01, 0x01, 0x00, 0x00, 0x00};
					break;
				case 5:
					CMD_DES_1[0] = (byte)ProtocolCmd.CMD_5;
					CMD_DES_2 = new byte[]{0x01, 0x01, 0x01, 0x64, 0x00, 0x00, 0xff, 0xff, 0xff};
					break;
				case 14:
					CMD_DES_1[0] = (byte)ProtocolCmd.CMD_R;
					CMD_DES_2 = new byte[]{0x01, 0x01, 0x01};
					break;
				case 17: // CMD_W
					CMD_DES_1[0] = (byte)ProtocolCmd.CMD_W;
					CMD_DES_2 = new byte[]{(byte)this.PACKET_NR};
					execCMD = true;
					break;
				case 18: // CMD_X   blank the display
					CMD_DES_1[0] = (byte)ProtocolCmd.CMD_X;
					CMD_DES_2 = new byte[]{0x01, 0x05};
					break;
				case 20: // CMD T
					CMD_DES_1[0] = (byte)ProtocolCmd.CMD_T;
					CMD_DES_2 = new byte[]{ 0x01, 0x77};
					//	binary_content = new byte[]{0x0D};
					break;
				case 21:   // CLEAR_RAM '>'
					CMD_DES_1[0] = (byte)ProtocolCmd.CMD_CLEAR_RAM;
					CMD_DES_2 = new byte[0];
					break;
			}
			if(binary_content != null)
			{
				DATA = binary_content;
			}
			else
			{
				DATA = new byte[0];         
			}
			buffer_size = (CMD_DES_1.Length+CMD_DES_2.Length+DATA.Length);
			BUFFER = new byte[buffer_size];
			Array.Copy(CMD_DES_1, 0, BUFFER, 0, CMD_DES_1.Length);
			Array.Copy(CMD_DES_2, 0, BUFFER, CMD_DES_1.Length, CMD_DES_2.Length);
			Array.Copy(DATA, 0, BUFFER, (CMD_DES_1.Length+CMD_DES_2.Length), DATA.Length);

			return plugIntoDataSkelekton(BUFFER, this.PACKET_NR);
		}
		
		byte[] loadTypicalContent(int type)
		{
			byte[] b = new byte[0];
			return b;
		}

	}

	/*  DATA-FORMAT	--> consider also here RF-dependant issues
	*	INIT					#PAKET_START #	master # packet nr.# DATA# check-sum #CR	# feed	# feed_ext	
	*	0x00, 0xff, 0x00, 0xff	#0x1B		#	1	   #	1	   # 1-n #		1	 #0x0D  # 0x0A	# 0x55 0x00
	*/
	class DataSkelekton : DataResponse 
	{
		public int TYPE;
		byte[] PACKET_DATA;
		int total_size;
		byte checksum;

		protected byte PAKET_START = 0x1B;
		protected byte CARRIAGE_RETURN = 0x0D;
		protected byte LINE_FEED = 0x0A;

		protected byte[] RF_COMM_INITIALIZATION = {0x00, 0xff, 0x00, 0xff}; // send prior field 1 if RF-communication is used
		protected byte[] LINE_FEED_EXT = {0x55, 0x00}; // send after field 9 (you can also just use LINE_FEED)

		static bool FIRST_FRAGMENT = true;

		public enum ProtocolCmd : byte
		{	
			LOAD_TEXT         = 0x30,
			LOAD_BITMAP    = 0x31,
			LOAD_PARTIAL_TEXT     = 0x32,
			CMD_3                 = 0x33,
			CMD_4                 = 0x34,
			CMD_5                 = 0x35,
			CMD_6                 = 0x36,
			CMD_7                 = 0x07,
			CMD_8                 = 0x08,
			CMD_9                 = 0x09,
			CMD_COLON             = 0x3B, //';'
			CMD_M                 = 0x4D, //'M',
			CMD_O                 = 0x4f, //'O',
			CMD_P                 = 0x50, //'P',
			CMD_R                 = 0x52, //'R',
			CMD_S                 = 0x53, //'S',
			CMD_V                 = 0x56, //'V',
			CMD_W                 = 0x57, //'W',
			CMD_X                 = 0x58, //'X',
			CMD_Z                 = 0x5A,  //'Z'
			CMD_T		      = 0x54,	//'T'
			CMD_CLEAR_RAM	      = 0x3e
		}

		public enum ErrorMsgs : uint 
		{
			NO_RESPONSE     = 0x3E8,
			NO_CONNECTION   = 0x3E9,
			ERROR_UNKNOWN   = 0x3EA
		}
    

		public enum PacketBaseConfig : byte 
		{
			master_address = (byte)1,      // 0-31 || 0-63
			packet_nr_1 = (byte)1,           // 0-255
			packet_nr_2 = (byte)2,           // 0-255
			packet_nr_3 = (byte)3,
			sense = 0x01
		}

		public DataSkelekton()
		{

		}

		public byte[] plugIntoDataSkelekton(byte[] data_core, int packet_nr)
		{
			byte[] hack = new byte[]{0x01, 0x01, 0x00, 0x00, 0x09, 0x60, 0xff, 0xff, 0xff, 0xff, 0xff};
			int pChecksum = 0;
			
			if(FIRST_FRAGMENT)
			{
				total_size = 7 + data_core.Length;	// was 5
				PACKET_DATA = new byte[total_size+6+1];
				PACKET_DATA[0] = RF_COMM_INITIALIZATION[0];	// 0
				PACKET_DATA[1] = RF_COMM_INITIALIZATION[1];	// 0
				PACKET_DATA[2] = RF_COMM_INITIALIZATION[2];
				PACKET_DATA[3] = RF_COMM_INITIALIZATION[3];
				PACKET_DATA[2] = PAKET_START;
				PACKET_DATA[3] = (byte)PacketBaseConfig.master_address;
				PACKET_DATA[4] = (byte)packet_nr;
				Array.Copy(data_core, 0, PACKET_DATA, 7, data_core.Length);  //total_size += data_core.Length;w
				pChecksum = total_size;
				total_size++;
			
				PACKET_DATA[total_size] = CARRIAGE_RETURN;                      total_size++;
				PACKET_DATA[total_size] = LINE_FEED;                            total_size++;
				PACKET_DATA[total_size] = LINE_FEED_EXT[0];						total_size++;
				PACKET_DATA[total_size] = LINE_FEED_EXT[1];						total_size++;
				//	PACKET_DATA[total_size] = LINE_FEED_EXT[1];						total_size++;
				PACKET_DATA[pChecksum] = checksum = computeChecksum(PACKET_DATA, 2, pChecksum);
				FIRST_FRAGMENT = false;
			}
			else
			{
				total_size = 3 + data_core.Length;
				PACKET_DATA = new byte[total_size+5+1];
				PACKET_DATA[0] = PAKET_START;
				PACKET_DATA[1] = (byte)PacketBaseConfig.master_address;
				PACKET_DATA[2] = (byte)packet_nr;
				Array.Copy(data_core, 0, PACKET_DATA, 3, data_core.Length);  //total_size += data_core.Length;
				pChecksum = total_size;
				total_size++;
				PACKET_DATA[total_size] = CARRIAGE_RETURN;                      total_size++;
				PACKET_DATA[total_size] = LINE_FEED;                            total_size++;
				PACKET_DATA[total_size] = LINE_FEED_EXT[0];						total_size++;
				PACKET_DATA[total_size] = LINE_FEED_EXT[1];						total_size++;
				PACKET_DATA[pChecksum] = checksum = computeChecksum(PACKET_DATA, 0, pChecksum);
			}
			return PACKET_DATA;
		}

		byte computeChecksum(byte[] data, int start, int pChecksum)
		{
			int x;
			int val = 0;
			for(x = start; x < pChecksum; x++)
			{
				val += (int)data[x];
			}
			return ((byte)(val & 0x000000ff));
		}
		int getTotalSize()
		{
			return total_size;
		}
                     
		byte getChecksum()
		{
			return checksum;
		}
	}
        

	internal class DataResponse
	{
		static byte RETURN_MSG_POSITIVE = 0x06;
		static byte RETURN_MSG_NEGATIVE = 0x15;

		byte negative = RETURN_MSG_NEGATIVE;

		int ERROR_CODE;

		public bool checkResponseMsg(byte[] response_msg)
		{
			/*
			if((response_msg[2].Equals(RETURN_MSG_POSITIVE)) || (response_msg[4].Equals(RETURN_MSG_POSITIVE)))
			{
				ERROR_CODE = (int)response_msg[2];
				return (response_msg[4] == RETURN_MSG_POSITIVE) ? true : false;
			
			}
			else
			{
				return false;
			}
			*/
			
			return true;
		}
	}
}


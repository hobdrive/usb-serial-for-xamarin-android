/* Copyright 2011-2013 Google Inc.
 * Copyright 2013 mike wakerly <opensource@hoho.com>
 * Copyright 2015 Yasuyuki Hamada <yasuyuki_hamada@agri-info-design.com>
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 2.1 of the License, or (at your option) any later version.
 *
 * This library is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public
 * License along with this library; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301,
 * USA.
 *
 * Project home page: https://github.com/ysykhmd/usb-serial-for-xamarin-android
 * 
 * This project is based on usb-serial-for-android and ported for Xamarin.Android.
 * Original project home page: https://github.com/mik3y/usb-serial-for-android
 */

using System;
using System.Threading;
using Android.Hardware.Usb;
using Android.Util;

#if UseSmartThreadPool
using Amib.Threading;
#endif

namespace Aid.UsbSerial
{
    public abstract class UsbSerialPort
    {
        private const string TAG = "UsbSerailPort";

        /**
         * InternalReadBuffer[] �ɂ���
         *  �EFtdiSerialPort.cs �ł����g���Ă��Ȃ�
         *  �EDEFAULT_TEMP_READ_BUFFER_SIZE �� DEFAULT_INTERNAL_READ_BUFFER_SIZE �̃T�C�Y�͓����ɂ��邱�ƁB
         */
        public const int DEFAULT_INTERNAL_READ_BUFFER_SIZE = 4 * 1024;
        /**
         * TempReadBuffer �̃T�C�Y�ɂ���
         *   Nexus5(Android 5.1.1/LMY48B)+FT-232RL �̑g�ݍ��킹��
         *     �E115200bps �ł���Ȃ�Ɉ��蓮�삳����ɂ́A������ InternalReadBuffer[] �� 1024 �ȏ�K�v
         *     �EFtdiSerialPort.cs �ŌĂяo���Ă��� Connection.BulkTransfer() �́A�o�b�t�@�̃T�C�Y�� 256 �̔{���ȊO�ł́A�{�[���[�g�� 57600bps �ȏ�ŃG���[��Ԃ��B�����͕s��
         *     �EInternalReadBuffer[] �́A�ő� 16384byte �܂ł����g���Ȃ�(������傫���Ă� 16384byte �𒴂��镔���͎g���Ȃ�)
         *     �E57600,115200bps �ł� InternalReadBuffer[] ��t�Ƀf�[�^���l�ߍ��܂�Ă��邱�Ƃ�����B���̂Ƃ�InternalReadBuffer[16384]�Ƃ���� 57,600bps �� 2.84..�b�A
         *       115200bps �� 1.422..�b�ƂȂ�B�f�[�^�Ɏ��ԏ����܂ޏꍇ�͗v����(DEFAULT_READ_TIMEOUT_MILLISEC = 0 �̏ꍇ)
         *     �EInternalReadBuffer[4096] �� 115200bps �� 0x00-0xFF �̃f�[�^��A����M�����ꍇ�A�����Ɉ�x�G���[���N����(30���ɂS��Ƃ�)�BInternalReadBuffer[] �̃T�C�Y
         *     �@�𒲐����Ă��A�󋵂��傫���ς�邱�Ƃ��Ȃ�����
         *   Nexus5(Android 5.1.1/LMY48B)+CP2102 �̑g�ݍ��킹��
         *     �EDEFAULT_TEMP_READ_BUFFER_SIZE = 16 * 1024 ���ƁA57600, 115200bps �Ńf�[�^����M�ł��Ȃ�(�����s��)
         *     �EDEFAULT_TEMP_READ_BUFFER_SIZE = 4 * 1024 ���ƁA4,800bps�A115200bps �Ƃ��Ƀf�[�^����M�ł���
         *     �EDEFAULT_TEMP_READ_BUFFER_SIZE = 4 * 1024 ���ƁA19,200bps �Ńf�[�^��M�C�x���g�̔��������� 2.133..�b �ƂȂ�(DEFAULT_READ_TIMEOUT_MILLISEC = 0 �̏ꍇ)
         *     �EDEFAULT_TEMP_READ_BUFFER_SIZE = 4 * 1024 ���ƁA38,400bps �Ńf�[�^��M�C�x���g�̔��������� 1.066..�b �ƂȂ�(DEFAULT_READ_TIMEOUT_MILLISEC = 0 �̏ꍇ)
         *   Nexus5(Android 5.1.1/LMY48B)+PL2303HXA, PL2303HXD �̑g�ݍ��킹��
         *     �EInternalReadBuffer[] �� 115200bps �ł��������� 1024byte ���x�����g���Ȃ���ۂ�����B���������ƂɃf�[�^�� Android OS ����オ���Ă���悤�ȓ���
         *   Nexus5(Android 5.1.1/LMY48B)+u-blox6 �̑g�ݍ��킹��
         *     �EInternalReadBuffer[] �� 115200bps �ł��������� 1024byte ���x�����g���Ȃ���ۂ�����B�ʏ�͈�����(4ms���x?)���ƂɃf�[�^�� Android OS ����オ���Ă���悤�ȓ���
         */
        // �ύX����ꍇ�� FtdiSerailPort.cs �� ReadInternal() ���� Connection.BulkTransfer() �Ăяo�������𒍈ӁB
        public const int DEFAULT_TEMP_READ_BUFFER_SIZE = DEFAULT_INTERNAL_READ_BUFFER_SIZE;
        // ���̒l���������ƁAFT-232R �œ]�����x�������Ƃ��A�ŏ��̃f�[�^���t�H�A�O���E���h����ǂ݂����Ȃ����Ƃ�����B
        public const int DEFAULT_READ_BUFFER_SIZE = 16 * 1024;
        public const int DEFAULT_WRITE_BUFFER_SIZE = 16 * 1024;
        // UsbSerailForAndroid �Ƃ��Ă̏����ݒ�l
        //   UsbSerialExamples �Ƒg�ݍ��킹��ꍇ�� SerialConsoleActivity.cs �� DEFAULT_TRANSFAR_RATE ���D�悳���
        public const int DefaultBaudrate = 9600;
        public const int DefaultDataBits = 8;
        public const Parity DefaultParity = Parity.None;
        public const StopBits DefaultStopBits = StopBits.One;

        // �f�[�^��M�^�C���A�E�g�̎w�� (ms)
        // Nexus5(Android 5.1.1/LMY48B)+FT232RL �̑g�ݍ��킹��
        //   500 ���� 0x00-0xff/ 57600, 115200bps ����M�ł��Ȃ�
        //   0 ���� 0x00-0xff/57600bps/innerBuffer 16384byte �Ńf�[�^��M�̃C�x���g�����̊Ԋu���R�b�߂��A115200 bps ���� 1.5�b���x�J�����Ƃ�����
        // Nexus5(Android 5.1.1/LMY48B)+CP2102 �̑g�ݍ��킹��
        //   �EDEFAULT_TEMP_READ_BUFFER_SIZE = 4 * 1024 ���ƁA19200bps �ł� 300 ���w�肷��Ǝ�M�ł��Ȃ�(9600bps �ł͎�M�ł���)
        // (const ���g���� CdcAcmSerailPort.cs �� ReadInternal() ���� warning �������Ȃ��̂ŁA�������� readonly ���g���Ă���)
        public readonly int DEFAULT_READ_TIMEOUT_MILLISEC = 0;

        public event EventHandler<DataReceivedEventArgs> DataReceivedEventLinser;

        protected int CurrentPortNumber;

        // non-null when open()
        protected UsbDeviceConnection Connection { get; set; }

        volatile protected Object MainReadBufferLock = new Object();
        volatile protected Object MainWriteBufferLock = new Object();

        // FtdiSerialPort.cs �ł����g���Ă��Ȃ�
        protected byte[] InternalReadBuffer;
        protected byte[] TempReadBuffer;
        /** Main read buffer.  Guarded by {@link #MainReadBufferLock}. */
        protected byte[] MainReadBuffer;
        protected int MainReadBufferWriteCursor;
        protected int MainReadBufferReadCursor;

        /** Internal write buffer.  Guarded by {@link #MainWriteBufferLock}. */
        protected byte[] MainWriteBuffer;

        private int dataBits;

        private volatile bool _ContinueUpdating;
        public bool IsOpened { get; protected set; }
        public int Baudrate { get; set; }
        public int DataBits
        {
            get { return dataBits; }
            set
            {
                if (value < 5 || 8 < value) { throw new ArgumentOutOfRangeException(); }
                dataBits = value;
            }
        }
        public Parity Parity { get; set; }
        public StopBits StopBits { get; set; }

#if UseSmartThreadPool
        public SmartThreadPool ThreadPool { get; set; }
#endif

#if UseSmartThreadPool
        public UsbSerialPort(UsbManager manager, UsbDevice device, int portNumber, SmartThreadPool threadPool)
#else
        public UsbSerialPort(UsbManager manager, UsbDevice device, int portNumber)
#endif
        {
            Baudrate = DefaultBaudrate;
            DataBits = DefaultDataBits;
            Parity = DefaultParity;
            StopBits = DefaultStopBits;

            UsbManager = manager;
            UsbDevice = device;
            CurrentPortNumber = portNumber;

            InternalReadBuffer = new byte[DEFAULT_INTERNAL_READ_BUFFER_SIZE];
            TempReadBuffer = new byte[DEFAULT_TEMP_READ_BUFFER_SIZE];
            MainReadBuffer = new byte[DEFAULT_READ_BUFFER_SIZE];
            MainReadBufferReadCursor = 0;
            MainReadBufferWriteCursor = 0;
            MainWriteBuffer = new byte[DEFAULT_WRITE_BUFFER_SIZE];

#if UseSmartThreadPool
            ThreadPool  = threadPool;
#endif
        }

        public override string ToString()
        {
            return string.Format("<{0} device_name={1} device_id={2} port_number={3}>", this.GetType().Name, UsbDevice.DeviceName, UsbDevice.DeviceId, CurrentPortNumber);
        }

        public UsbManager UsbManager
        {
            get; private set;
        }

        /**
         * Returns the currently-bound USB device.
         *
         * @return the device
         */
        public UsbDevice UsbDevice
        {
            get; private set;
        }

        /**
         * Sets the size of the internal buffer used to exchange data with the USB
         * stack for read operations.  Most users should not need to change this.
         * 
         * �ύX���͎�M�f�[�^������̂ŗv����
         *
         * @param bufferSize the size in bytes
         */
        public void SetReadBufferSize(int bufferSize)
        {
            if (bufferSize == InternalReadBuffer.Length)
            {
                return;
            }
            InternalReadBuffer = new byte[bufferSize];
        }

        /**
         * Sets the size of the internal buffer used to exchange data with the USB
         * stack for write operations.  Most users should not need to change this.
         *
         * @param bufferSize the size in bytes
         */
        public void SetWriteBufferSize(int bufferSize)
        {
            lock (MainWriteBufferLock)
            {
                if (bufferSize == MainWriteBuffer.Length)
                {
                    return;
                }
                MainWriteBuffer = new byte[bufferSize];
            }
        }

        // Members of IUsbSerialPort

        public int PortNumber
        {
            get { return CurrentPortNumber; }
        }

        /**
         * Returns the device serial number
         *  @return serial number
         */
        public string Serial
        {
            get { return Connection != null ? Connection.Serial : null; }
        }


        public abstract void Open();

        public abstract void Close();


        protected void CreateConnection()
        {
            if (UsbManager != null && UsbDevice != null)
            {
                lock (MainReadBufferLock)
                {
                    lock (MainWriteBufferLock)
                    {
                        Connection = UsbManager.OpenDevice(UsbDevice);
                    }
                }
            }
        }


        protected void CloseConnection()
        {
            if (Connection != null)
            {
                lock (MainReadBufferLock)
                {
                    lock (MainWriteBufferLock)
                    {
                        Connection.Close();
                        Connection = null;
                    }
                }
            }
        }


        protected void StartUpdating()
        {
#if UseSmartThreadPool
            if (ThreadPool != null)
            {
                ThreadPool.QueueWorkItem(o => DoTasks());
            }
            else
            {
                System.Threading.ThreadPool.QueueUserWorkItem(o => DoTasks());
            }
#else
            ThreadPool.QueueUserWorkItem(o => DoTasks());
#endif
        }


        protected void StopUpdating()
        {
            _ContinueUpdating = false;
        }

#if UseSmartThreadPool
        private object DoTasks()
#else
        /*
         * MainReadBuffer �� TempReadBuffer ���傫������
         */
        private WaitCallback DoTasks()
#endif
        {
            int doTaskRxLen;
            int readRemainBufferSize;

            _ContinueUpdating = true;
            try
            {
                while (_ContinueUpdating)
                {
                    doTaskRxLen = ReadInternal();

                    // �f�o�b�O���̃��O�o�͗p�R�[�h:
                    //  ReadInternal() �̓Ǐo���o�C�g���� ��萔�𒴂����ꍇ�ɁA����ȍ~20��AReadInternal() ���ǂ݂������f�[�^���_���v����
                    //  while �̊O�� next ��錾����K�v������
                    //  ProlificSerialPort.cs, CdcAcmSerailPort.cs �̎�M�G���[�����Ɏg�p����
                    //if (doTaskRxLen >= 256 || next > 0)   // ProlificSerialPort.cs 115200bps
                    //if (doTaskRxLen >= 60 || next > 0)    // CdcAcmSerailPort.cs 115200bps
                    //{
                    //    string msg = "";
                    //    for (int i = 0; i < doTaskRxLen; i++)
                    //    {
                    //        msg += TempReadBuffer[i].ToString("x2") + " ";
                    //    }
                    //    Log.Info(TAG, "Read Data Length : " + doTaskRxLen.ToString() + "\n" + msg);
                    //    if (0 == next)
                    //    {
                    //        next = 20;
                    //    }
                    //    else
                    //    {
                    //        next -= 1;
                    //    }
                    //}

                    // �f�o�b�O���̃��O�o�͗p�R�[�h:ReadInternal() �����ɓǂݏo�� byte�������O�ɏo��
                    // Log.Info(TAG, "Read Data Length : " + DateTime.Now.ToString("HH:mm:ss.fff") + ":" + doTaskRxLen.ToString() + "\n");

                    if (doTaskRxLen > 0)
                    {
                        lock (MainReadBufferLock)
                        {
                            readRemainBufferSize = DEFAULT_READ_BUFFER_SIZE - MainReadBufferWriteCursor;

                            if (doTaskRxLen > readRemainBufferSize)
                            {
                                Array.Copy(TempReadBuffer, 0, MainReadBuffer, MainReadBufferWriteCursor, readRemainBufferSize);
                                MainReadBufferWriteCursor = doTaskRxLen - readRemainBufferSize;
                                Array.Copy(TempReadBuffer, readRemainBufferSize, MainReadBuffer, 0, MainReadBufferWriteCursor);
                            }
                            else
                            {
                                Array.Copy(TempReadBuffer, 0, MainReadBuffer, MainReadBufferWriteCursor, doTaskRxLen);
                                MainReadBufferWriteCursor += doTaskRxLen;
                                if (DEFAULT_READ_BUFFER_SIZE == MainReadBufferWriteCursor)
                                {
                                    MainReadBufferWriteCursor = 0;
                                }
                            }
                            if (DataReceivedEventLinser != null)
                            {
                                DataReceivedEventLinser(this, new DataReceivedEventArgs(this));
                            }
                        }
                    }
                }
            }
            catch (SystemException e)
            {
                Log.Error(TAG, "Data read faild: " + e.Message, e);
                _ContinueUpdating = false;
                Close();
                throw new System.IO.IOException("XXXX");
            }
            return null;
        }

        /*
         * �K�x�[�W�𑝂₳�Ȃ����߂Ɋ֐����̎����ϐ��́A���ׂĊ֐��O�Ő錾����
         */
        int readFirstLength;
        int readValidDataLength;
        public int Read(byte[] dest, int startIndex)
        {
            // MainReadBuffer[] �ɃA�N�Z�X����̂ŁA�����Ƀ��b�N�͕K�v
            lock (MainReadBufferLock)
            {
                readValidDataLength = MainReadBufferWriteCursor - MainReadBufferReadCursor;
                if (readValidDataLength != 0)
                {
                    /*
                     * �ȉ��͍������̂��߂ɈӐ}�I�Ɋ֐��������Ă��Ȃ�
                     */
                    if (MainReadBufferWriteCursor < MainReadBufferReadCursor)
                    {
                        readValidDataLength += DEFAULT_READ_BUFFER_SIZE;
                        if (readValidDataLength > dest.Length)
                        {
                            readValidDataLength = dest.Length;
                        }

                        if (readValidDataLength + MainReadBufferReadCursor > DEFAULT_READ_BUFFER_SIZE)
                        {
                            readFirstLength = DEFAULT_READ_BUFFER_SIZE - MainReadBufferReadCursor;

                            Array.Copy(MainReadBuffer, MainReadBufferReadCursor, dest, startIndex, readFirstLength);
                            MainReadBufferReadCursor = readValidDataLength - readFirstLength;
                            Array.Copy(MainReadBuffer, 0, dest, startIndex + readFirstLength, MainReadBufferReadCursor);
                        }
                        else
                        {
                            Array.Copy(MainReadBuffer, MainReadBufferReadCursor, dest, startIndex, readValidDataLength);
                            MainReadBufferReadCursor += readValidDataLength;
                            if (DEFAULT_READ_BUFFER_SIZE == MainReadBufferReadCursor)
                            {
                                MainReadBufferReadCursor = 0;
                            }
                        }
                    }
                    else
                    {
                        if (readValidDataLength > dest.Length)
                        {
                            readValidDataLength = dest.Length;
                        }

                        Array.Copy(MainReadBuffer, MainReadBufferReadCursor, dest, startIndex, readValidDataLength);
                        MainReadBufferReadCursor += readValidDataLength;
                        if (DEFAULT_READ_BUFFER_SIZE == MainReadBufferReadCursor)
                        {
                            MainReadBufferReadCursor = 0;
                        }
                    }
                }
            }
            return readValidDataLength;
        }

        public void ResetParameters()
        {
            SetParameters(Baudrate, DataBits, StopBits, Parity);
        }

        public void ResetReadBuffer()
        {
            lock(MainReadBufferLock)
            {
                MainReadBufferReadCursor = 0;
                MainReadBufferWriteCursor = 0;
            }
        }

        protected abstract int ReadInternal();

        public abstract int Write(byte[] src, int timeoutMillis);

        protected abstract void SetParameters(int baudRate, int dataBits, StopBits stopBits, Parity parity);

        public abstract bool CD { get; }

        public abstract bool Cts { get; }

        public abstract bool Dsr { get; }

        public abstract bool Dtr { get; set; }

        public abstract bool RI { get; }

        public abstract bool Rts { get; set; }

        public virtual bool PurgeHwBuffers(bool flushReadBuffers, bool flushWriteBuffers)
        {
            return !flushReadBuffers && !flushWriteBuffers;
        }
    }
}


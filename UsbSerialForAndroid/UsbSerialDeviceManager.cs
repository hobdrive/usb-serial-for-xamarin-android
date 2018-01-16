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
using System.Collections.Generic;

using Android.App;
using Android.Content;
using Android.Hardware.Usb;

#if UseSmartThreadPool
using Amib.Threading;
#endif

namespace Aid.UsbSerial
{
    /**
     *
     * @author Yasuyuki Hamada (yasuyuki_hamada@agri-info-design.com)
     */
    public class UsbSerialDeviceManager
    {
        private Context Context { get; set; }
        private bool IsWorking { get; set; }
        private UsbManager UsbManager { get; set; }
        private UsbSerialDeviceBroadcastReceiver Receiver { get; set; }
        private string ActionUsbPermission { get; set; }
        public bool AllowAnonymousCdcAcmDevices { get; private set; }
#if UseSmartThreadPool
        public SmartThreadPool ThreadPool { get; private set; }
#endif

        public event EventHandler<UsbSerialDeviceEventArgs> DeviceAttached;
        public event EventHandler<UsbSerialDeviceEventArgs> DeviceDetached;

        public Dictionary<UsbSerialDeviceID, UsbSerialDeviceInfo> AvailableDeviceInfo { get; private set;}
        private readonly object _attachedDevicesSyncRoot = new object();
        public List<UsbSerialDevice> AttachedDevices { get; private set;}

#if UseSmartThreadPool
        public UsbSerialDeviceManager(Context context, string actionUsbPermission, bool allowAnonymousCdcAmcDevices)
            : this(context, actionUsbPermission, allowAnonymousCdcAmcDevices, UsbSerialDeviceList.Default, null)
        {
        }

        public UsbSerialDeviceManager(Context context, string actionUsbPermission, bool allowAnonymousCdcAmcDevices, SmartThreadPool threadPool)
            : this(context, actionUsbPermission, allowAnonymousCdcAmcDevices, UsbSerialDeviceList.Default, threadPool)
        {
        }
#else
        public UsbSerialDeviceManager(Context context, string actionUsbPermission, bool allowAnonymousCdcAmcDevices)
            : this(context, actionUsbPermission, allowAnonymousCdcAmcDevices, UsbSerialDeviceList.Default)
        {
        }
#endif

#if UseSmartThreadPool
        public UsbSerialDeviceManager(Context context, string actionUsbPermission, bool allowAnonymousCdcAmcDevices, UsbSerialDeviceList availableDeviceList, SmartThreadPool threadPool)
#else
        public UsbSerialDeviceManager(Context context, string actionUsbPermission, bool allowAnonymousCdcAmcDevices, UsbSerialDeviceList availableDeviceList)
#endif
        {
            if (context == null)
                throw new ArgumentNullException();
            if (string.IsNullOrEmpty(actionUsbPermission))
                throw new ArgumentException();
            if (availableDeviceList == null)
                throw new ArgumentNullException();

            Context = context;
            ActionUsbPermission = actionUsbPermission;
            UsbManager = (UsbManager)context.GetSystemService(Context.UsbService);
            Receiver = new UsbSerialDeviceBroadcastReceiver(this, UsbManager, actionUsbPermission);
            AllowAnonymousCdcAcmDevices = allowAnonymousCdcAmcDevices;

            AvailableDeviceInfo = availableDeviceList.AvailableDeviceInfo;
            AttachedDevices = new List<UsbSerialDevice>();

#if UseSmartThreadPool
            ThreadPool = threadPool;
#endif
        }

        public void Start()
        {
            Android.Util.Log.Info("USB", "Class: USBSerialDeviceManager | Method: Start()");

            if (IsWorking)
            {
                return;
            }
            IsWorking = true;
            // listen for new devices
            var filter = new IntentFilter();
            filter.AddAction(UsbManager.ActionUsbDeviceAttached);
            filter.AddAction(UsbManager.ActionUsbDeviceDetached);
            filter.AddAction(ActionUsbPermission);
            Context.RegisterReceiver(Receiver, filter);

            Android.Util.Log.Info("USB", "Class: USBSerialDeviceManager | Method: Start() | Commect: Выполняем Update()");

            Update();
        }

        internal void AddDevice(UsbManager usbManager, UsbDevice usbDevice)
        {
            Android.Util.Log.Info("USB", "Class: USBSerialDeviceManager | Method: AddDevice()");

            var serialDevice = GetDevice(usbManager, usbDevice, AllowAnonymousCdcAcmDevices);
            if (serialDevice != null)
            {
                lock (_attachedDevicesSyncRoot)
                {
                    AttachedDevices.Add(serialDevice);
                    if (DeviceAttached != null)
                    DeviceAttached.Invoke(this, new UsbSerialDeviceEventArgs(serialDevice));
                }
            }
        }

        internal void RemoveDevice(UsbDevice usbDevice)
        {
            UsbSerialDevice removedDevice = null;
            var attachedDevices = AttachedDevices.ToArray();
            foreach (var device in attachedDevices)
            {
                bool serialEquals = true;
                if(Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.Lollipop)
                    serialEquals = device.UsbDevice.SerialNumber == usbDevice.SerialNumber;

                if (device.UsbDevice.VendorId == usbDevice.VendorId
                    && device.UsbDevice.ProductId == usbDevice.ProductId
                    && serialEquals)
                {
                    removedDevice = device;
                    break;
                }
            }
            if (removedDevice != null)
            {
                RemoveDevice(removedDevice);
            }
        }

        internal void RemoveDevice(UsbSerialDevice serialDevice)
        {
            Android.Util.Log.Info("USB", "Class: USBSerialDeviceManager | Method: RemoveDevice()");

            if (serialDevice != null)
            {
                lock (_attachedDevicesSyncRoot)
                {
                    Android.Util.Log.Info("USB", "Class: USBSerialDeviceManager | Method: RemoveDevice() | Comment: Закрываем все порты");

                    serialDevice.CloseAllPorts();
                    if (DeviceDetached != null)
                        DeviceDetached.Invoke(this, new UsbSerialDeviceEventArgs(serialDevice));
                    AttachedDevices.Remove(serialDevice);
                }
            }
        }

        public void Stop()
        {
            Android.Util.Log.Info("USB", "Class: USBSerialDeviceManager | Method: Stop()");

            if (!IsWorking)
            {
                return;
            }
            IsWorking = false;
            var attachedDevices = AttachedDevices.ToArray();
            foreach (var device in attachedDevices)
            {
                RemoveDevice(device);
            }
            Context.UnregisterReceiver(Receiver);
        }

        public void Update()
        {
            Android.Util.Log.Info("USB", "Class: USBSerialDeviceManager | Method: Update()");

            // Remove detached devices from AttachedDevices
            var attachedDevices = AttachedDevices.ToArray();
            foreach (var attachedDevice in attachedDevices)
            {
                Android.Util.Log.Info("USB", "Class: USBSerialDeviceManager | Method: Update() | Comment: в AttachedDevices есть девайсы. Проходим циклом по всем девайсам");

                var exists = false;
                foreach (var usbDevice in UsbManager.DeviceList.Values)
                {
                    Android.Util.Log.Info("USB", "Class: USBSerialDeviceManager | Method: Update() | Comment: в UsbManager.DeviceList тоже есть девайсы. Проходим циклом по всем девайсам");

                    bool serialEquals = true;
                    if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.Lollipop)
                        serialEquals = usbDevice.SerialNumber == attachedDevice.UsbDevice.SerialNumber;

                    if ((usbDevice.VendorId == attachedDevice.ID.VendorID) && 
                        (usbDevice.ProductId == attachedDevice.ID.ProductID) &&
                        serialEquals)
                    {
                        Android.Util.Log.Info("USB", "Class: USBSerialDeviceManager | Method: Update() | Comment: Обнаружено совпадение.Выходим из циклов");
                        exists = true;
                        break;
                    }
                }
                if (!exists)
                {
                    Android.Util.Log.Info("USB", "Class: USBSerialDeviceManager | Method: Update() | Comment: Совпадений не обнаружено. Удаляем девайс");

                    RemoveDevice(attachedDevice);
                }
            }

            Android.Util.Log.Info("USB", "Class: USBSerialDeviceManager | Method: Update() | Comment: Переходим ко второму циклу");

            // Add attached devices If not exists in AttachedDevices
            foreach (var usbDevice in UsbManager.DeviceList.Values)
            {
                Android.Util.Log.Info("USB", "Class: USBSerialDeviceManager | Method: Update() | Comment: в UsbManager.DeviceList есть девайсы. Проходим циклом по всем девайсам");

                var exists = false;
                attachedDevices = AttachedDevices.ToArray();
                foreach (var attachedDevice in attachedDevices)
                {
                    Android.Util.Log.Info("USB", "Class: USBSerialDeviceManager | Method: Update() | Comment: в AttachedDevices тоже есть девайсы. Проходим циклом по всем девайсам");

                    bool serialEquals = true;
                    if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.Lollipop)
                        serialEquals = usbDevice.SerialNumber == attachedDevice.UsbDevice.SerialNumber;

                    if ((usbDevice.VendorId == attachedDevice.ID.VendorID) && 
                        (usbDevice.ProductId == attachedDevice.ID.ProductID) &&
                        serialEquals)
                    {
                        Android.Util.Log.Info("USB", "Class: USBSerialDeviceManager | Method: Update() | Comment: Обнаружено совпадение");

                        exists = true;
                        break;
                    }
                }
                if (exists)
                {
                    Android.Util.Log.Info("USB", "Class: USBSerialDeviceManager | Method: Update() | Comment: Выходим из цикла, не выводя окно на запрос прав доступа");
                    break;
                }

                Android.Util.Log.Info("USB", "Class: USBSerialDeviceManager | Method: Update() | Comment: Обнаружений не найдено. Значит будем проверять права подключенного устройства... ");

                if (!UsbManager.HasPermission(usbDevice))
                {
                    Android.Util.Log.Info("USB", "Class: USBSerialDeviceManager | Method: Update() | Comment: У подключенного устройства, права не обнаружены. Поэтому выводим окно с запросом ");

                    var permissionIntent = PendingIntent.GetBroadcast(Context.ApplicationContext, 0, new Intent(ActionUsbPermission), 0);
                    UsbManager.RequestPermission(usbDevice, permissionIntent);
                }
                else
                {
                    Android.Util.Log.Info("USB", "Class: USBSerialDeviceManager | Method: Update() | Comment: У подключенного устройства уже есть права. Поэтому автоматически его добавляем в AttachedDevices ");

                    AddDevice(UsbManager, usbDevice);
                }
            }
        }

        private UsbSerialDevice GetDevice(UsbManager usbManager, UsbDevice usbDevice, bool allowAnonymousCdcAcmDevices)
        {
            Android.Util.Log.Info("USB", "Class: USBSerialDeviceManager | Method: GetDevice()");

            var id = new UsbSerialDeviceID(usbDevice.VendorId, usbDevice.ProductId);
            var info = FindDeviceInfo(id, usbDevice.DeviceClass, allowAnonymousCdcAcmDevices);
            if (info != null)
            {
#if UseSmartThreadPool
                var device = new UsbSerialDevice(usbManager, usbDevice, id, info, ThreadPool);
#else
                var device = new UsbSerialDevice(usbManager, usbDevice, id, info);
#endif
                return device;
            }
            return null;
        }

        private UsbSerialDeviceInfo FindDeviceInfo(UsbSerialDeviceID id, UsbClass usbClass, bool allowAnonymousCdcAcmDevices)
        {
            Android.Util.Log.Info("USB", "Class: USBSerialDeviceManager | Method: FindDeviceInfo()");

            if (AvailableDeviceInfo.ContainsKey(id))
            {
                return AvailableDeviceInfo[id];
            }
            if (allowAnonymousCdcAcmDevices && usbClass == UsbClass.Comm)
            {
                return UsbSerialDeviceInfo.CdcAcm;
            }

            return null;
        }

        private class UsbSerialDeviceBroadcastReceiver : BroadcastReceiver
        {
            private UsbManager UsbManager { get; set;}

            private UsbSerialDeviceManager DeviceManager { get; set; }

            private string ActionUsbPermission { get; set; }

            public UsbSerialDeviceBroadcastReceiver(UsbSerialDeviceManager manager, UsbManager usbManager, string actionUsbPermission)
            {
                DeviceManager = manager;
                UsbManager = usbManager;
                ActionUsbPermission = actionUsbPermission;
            }

            public override void OnReceive(Context context, Intent intent)
            {
                Android.Util.Log.Info("USB", "Class: UsbSerialDeviceBroadcastReceiver | Method: OnRecieve() | Intent.Action : " + intent.Action);

                var device = intent.GetParcelableExtra(UsbManager.ExtraDevice) as UsbDevice;
                if (device == null)
                {
                    Android.Util.Log.Info("USB", "Class: UsbSerialDeviceBroadcastReceiver | Method: OnRecieve() | Comment: Девайс не обнаружен. Выходим из метода");

                    return;
                }

                var id = new UsbSerialDeviceID(device.VendorId, device.ProductId);
                var info = DeviceManager.FindDeviceInfo(id, device.DeviceClass, DeviceManager.AllowAnonymousCdcAcmDevices);
                if (info == null)
                {
                    Android.Util.Log.Info("USB", "Class: UsbSerialDeviceBroadcastReceiver | Method: OnRecieve() | Comment: Девайс обнаружен, но информации о нем нет. Выходим из метода");
                    return;
                }


                var action = intent.Action;
                if (action == UsbManager.ActionUsbDeviceAttached)
                {
                    Android.Util.Log.Info("USB", "Class: UsbSerialDeviceBroadcastReceiver | Method: OnRecieve() | Comment: Девайс вызвал событие Attached. Проверяем есть ли у подключенного устр-ва права...");

                    if (!UsbManager.HasPermission(device))
                    {
                        Android.Util.Log.Info("USB", "Class: UsbSerialDeviceBroadcastReceiver | Method: OnRecieve() | Comment: Прав нет. Вызываем окно с запросом на предоставление прав...");

                        var permissionIntent = PendingIntent.GetBroadcast(context, 0, new Intent(ActionUsbPermission), 0);
                        UsbManager.RequestPermission(device, permissionIntent);
                    }
                    else
                    {
                        Android.Util.Log.Info("USB", "Class: UsbSerialDeviceBroadcastReceiver | Method: OnRecieve() | Comment: Права у только что подключенного устройства есть. Добавляем его в DeviceManager (AttachedDevices) ");

                        DeviceManager.AddDevice(UsbManager, device);
                    }
                }
                else if (action == UsbManager.ActionUsbDeviceDetached)
                {
                    Android.Util.Log.Info("USB", "Class: UsbSerialDeviceBroadcastReceiver | Method: OnRecieve() | Comment: Устр-во было изъято. Удаляем его из DeviceManager (AttachedDevices) ");

                    DeviceManager.RemoveDevice(device);
                }
                else if (action == ActionUsbPermission)
                {
                    Android.Util.Log.Info("USB", "Class: UsbSerialDeviceBroadcastReceiver | Method: OnRecieve() | Comment: Пользователь выбрал в окне дать права устройству или нет. Узнаем что выбрал");

                    if (UsbManager.HasPermission(device))
                    {
                        Android.Util.Log.Info("USB", "Class: UsbSerialDeviceBroadcastReceiver | Method: OnRecieve() | Comment: Пользователь нажал 'ОК', тем самым разрешил устр-ву доступ. Теперь добавляем его в DeviceManager (AttachedDevices)");

                        DeviceManager.AddDevice(UsbManager, device);
                    }
                    else
                    {
                        Android.Util.Log.Info("USB", "Class: UsbSerialDeviceBroadcastReceiver | Method: OnRecieve() | Comment: Пользователь нажал 'Отмена', тем самым не дал права устройству. Никаких действий пока не предпринимаем");
                    }
                }
            }
        }
    }
}
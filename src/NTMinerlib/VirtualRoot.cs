﻿using LiteDB;
using NTMiner.Bus;
using NTMiner.Bus.DirectBus;
using NTMiner.Ip;
using NTMiner.Ip.Impl;
using NTMiner.MinerClient;
using NTMiner.Serialization;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;

namespace NTMiner {
    /// <summary>
    /// 虚拟根是0，是纯静态的，是先天地而存在的。
    /// </summary>
    public static partial class VirtualRoot {
        public static readonly string AppFileFullName = Process.GetCurrentProcess().MainModule.FileName;
        public static readonly string WorkerEventDbFileFullName = Path.Combine(MainAssemblyInfo.TempDirFullName, "workerEvent.litedb");
        public static Guid Id { get; private set; }
        
        #region IsMinerClient
        private static bool _isMinerClient;
        private static bool _isMinerClientDetected = false;
        private static readonly object _isMinerClientLocker = new object();
        public static bool IsMinerClient {
            get {
                if (_isMinerClientDetected) {
                    return _isMinerClient;
                }
                lock (_isMinerClientLocker) {
                    if (_isMinerClientDetected) {
                        return _isMinerClient;
                    }
                    // 基于约定
                    _isMinerClient = Assembly.GetEntryAssembly().GetManifestResourceInfo("NTMiner.Daemon.NTMinerDaemon.exe") != null;
                    _isMinerClientDetected = true;
                }
                return _isMinerClient;
            }
        }
        #endregion

        #region IsMinerStudio
        private static bool _isMinerStudio;
        private static bool _isMinerStudioDetected = false;
        private static readonly object _isMinerStudioLocker = new object();
        public static bool IsMinerStudio {
            get {
                if (_isMinerStudioDetected) {
                    return _isMinerStudio;
                }
                lock (_isMinerStudioLocker) {
                    if (_isMinerStudioDetected) {
                        return _isMinerStudio;
                    }
                    if (Environment.CommandLine.IndexOf("--minerstudio", StringComparison.OrdinalIgnoreCase) != -1) {
                        _isMinerStudio = true;
                    }
                    else {
                        // 基于约定
                        var assembly = Assembly.GetEntryAssembly();
                        // 单元测试时assembly为null
                        if (assembly == null) {
                            return false;
                        }
                        _isMinerStudio = assembly.GetManifestResourceInfo("NTMiner.NTMinerServices.NTMinerServices.exe") != null;
                    }
                    _isMinerStudioDetected = true;
                }
                return _isMinerStudio;
            }
        }
        #endregion

        public static ILocalIpSet LocalIpSet { get; private set; }
        public static IObjectSerializer JsonSerializer { get; private set; }

        public static readonly IMessageDispatcher SMessageDispatcher;
        private static readonly ICmdBus SCommandBus;
        private static readonly IEventBus SEventBus;
        public static readonly WorkerEventSet WorkerEvents;
        #region Out
        private static IOut _out;
        /// <summary>
        /// 输出到系统之外去
        /// </summary>
        public static IOut Out {
            get {
                return _out ?? EmptyOut.Instance;
            }
        }

        #region 这是一个外部不需要知道的类型
        private class EmptyOut : IOut {
            public static readonly EmptyOut Instance = new EmptyOut();

            private EmptyOut() { }

            public void ShowErrorMessage(string message, int? delaySeconds = null) {
                // nothing need todo
            }

            public void ShowInfo(string message) {
                // nothing need todo
            }

            public void ShowSuccessMessage(string message, string header = "成功") {
                // nothing need todo
            }
        }
        #endregion

        public static void SetOut(IOut ntOut) {
            _out = ntOut;
        }
        #endregion

        static VirtualRoot() {
            Id = NTMinerRegistry.GetClientId();
            LocalIpSet = new LocalIpSet();
            JsonSerializer = new ObjectJsonSerializer();
            SMessageDispatcher = new MessageDispatcher();
            SCommandBus = new DirectCommandBus(SMessageDispatcher);
            SEventBus = new DirectEventBus(SMessageDispatcher);
            WorkerEvents = new WorkerEventSet();
        }

        #region ConvertToGuid
        public static Guid ConvertToGuid(object obj) {
            if (obj == null) {
                return Guid.Empty;
            }
            if (obj is Guid guid1) {
                return guid1;
            }
            if (obj is string s) {
                if (Guid.TryParse(s, out Guid guid)) {
                    return guid;
                }
            }
            return Guid.Empty;
        }
        #endregion

        #region TagBrandId
        public static void TagBrandId(string brandKeyword, Guid brandId, string inputFileFullName, string outFileFullName) {
            string brand = $"{brandKeyword}{brandId}{brandKeyword}";
            string rawBrand = $"{brandKeyword}{GetBrandId(inputFileFullName, brandKeyword)}{brandKeyword}";
            byte[] data = Encoding.UTF8.GetBytes(brand);
            byte[] rawData = Encoding.UTF8.GetBytes(rawBrand);
            if (data.Length != rawData.Length) {
                throw new InvalidProgramException();
            }
            byte[] source = File.ReadAllBytes(inputFileFullName);
            int index = 0;
            for (int i = 0; i < source.Length - rawData.Length; i++) {
                int j = 0;
                for (; j < rawData.Length; j++) {
                    if (source[i + j] != rawData[j]) {
                        break;
                    }
                }
                if (j == rawData.Length) {
                    index = i;
                    break;
                }
            }
            for (int i = index; i < index + data.Length; i++) {
                source[i] = data[i - index];
            }
            File.WriteAllBytes(outFileFullName, source);
        }
        #endregion

        #region GetBrandId
        public static Guid GetBrandId(string fileFullName, string keyword) {
#if DEBUG
            Write.Stopwatch.Restart();
#endif
            Guid guid = Guid.Empty;
            int LEN = keyword.Length;
            if (fileFullName == AppFileFullName) {
                Assembly assembly = Assembly.GetEntryAssembly();
                string name = $"NTMiner.Brand.{keyword}";
                using (var stream = assembly.GetManifestResourceStream(name)) {
                    if (stream == null) {
                        return guid;
                    }
                    byte[] data = new byte[stream.Length];
                    stream.Read(data, 0, data.Length);
                    string rawBrand = Encoding.UTF8.GetString(data);
                    string guidString = rawBrand.Substring(LEN, rawBrand.Length - 2 * LEN);
                    Guid.TryParse(guidString, out guid);
                }
            }
            else {
                string rawBrand = $"{keyword}{Guid.Empty}{keyword}";
                byte[] rawData = Encoding.UTF8.GetBytes(rawBrand);
                int len = rawData.Length;
                byte[] source = File.ReadAllBytes(fileFullName);
                int index = 0;
                for (int i = 0; i < source.Length - len; i++) {
                    int j = 0;
                    for (; j < len; j++) {
                        if ((j < LEN || j > len - LEN) && source[i + j] != rawData[j]) {
                            break;
                        }
                    }
                    if (j == rawData.Length) {
                        index = i;
                        break;
                    }
                }
                string guidString = Encoding.UTF8.GetString(source, index + LEN, len - 2 * LEN);
                Guid.TryParse(guidString, out guid);
            }
#if DEBUG
            Write.DevTimeSpan($"耗时{Write.Stopwatch.ElapsedMilliseconds}毫秒 {typeof(VirtualRoot).Name}.GetBrandId");
#endif
            return guid;
        }
        #endregion

        public static void WorkerEvent(WorkerMessageChannel channel, string provider, WorkerMessageType eventType, string content) {
            WorkerEvents.Add(channel.GetName(), provider, eventType.GetName(), content);
        }

        public static WebClient CreateWebClient(int timeoutSeconds = 180) {
            return new NTMinerWebClient(timeoutSeconds);
        }

        #region 内部类
        public class WorkerEventSet : IEnumerable<IWorkerMessage> {
            private readonly string _connectionString;
            private readonly LinkedList<WorkerMessageData> _records = new LinkedList<WorkerMessageData>();

            internal WorkerEventSet() {
                _connectionString = $"filename={WorkerEventDbFileFullName};journal=false";
            }

            public int Count {
                get {
                    InitOnece();
                    return _records.Count;
                }
            }

            public void Add(string channel, string provider, string eventType, string content) {
                InitOnece();
                var data = new WorkerMessageData {
                    Id = Guid.NewGuid(),
                    Channel = channel,
                    Provider = provider,
                    MessageType = eventType,
                    Content = content,
                    EventOn = DateTime.Now
                };
                lock (_locker) {
                    _records.AddFirst(data);
                    while (_records.Count > WorkerEventSetCapacity) {
                        var toRemove = _records.Last;
                        _records.RemoveLast();
                        using (LiteDatabase db = new LiteDatabase(_connectionString)) {
                            var col = db.GetCollection<WorkerMessageData>();
                            col.Delete(toRemove.Value.Id);
                        }
                    }
                }
                using (LiteDatabase db = new LiteDatabase(_connectionString)) {
                    var col = db.GetCollection<WorkerMessageData>();
                    col.Insert(data);
                }
                Happened(new WorkerEvent(data));
            }

            private bool _isInited = false;
            private readonly object _locker = new object();

            private void InitOnece() {
                if (_isInited) {
                    return;
                }
                Init();
            }

            private void Init() {
                lock (_locker) {
                    if (!_isInited) {
                        using (LiteDatabase db = new LiteDatabase(_connectionString)) {
                            var col = db.GetCollection<WorkerMessageData>();
                            foreach (var item in col.FindAll().OrderBy(a => a.EventOn)) {
                                if (_records.Count < WorkerEventSetCapacity) {
                                    _records.AddFirst(item);
                                }
                                else {
                                    col.Delete(item.Id);
                                }
                            }
                        }
                        _isInited = true;
                    }
                }
            }

            public IEnumerator<IWorkerMessage> GetEnumerator() {
                InitOnece();
                return _records.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator() {
                InitOnece();
                return _records.GetEnumerator();
            }
        }

        private class NTMinerWebClient : WebClient {
            /// <summary>
            /// 单位秒，默认60秒
            /// </summary>
            public int TimeoutSeconds { get; set; }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="timeoutSeconds">秒</param>
            public NTMinerWebClient(int timeoutSeconds) {
                this.TimeoutSeconds = timeoutSeconds;
            }

            protected override WebRequest GetWebRequest(Uri address) {
                var result = base.GetWebRequest(address);
                result.Timeout = this.TimeoutSeconds * 1000;
                return result;
            }
        }
        #endregion
    }
}

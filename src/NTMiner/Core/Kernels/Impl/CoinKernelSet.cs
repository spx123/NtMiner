﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace NTMiner.Core.Kernels.Impl {
    internal class CoinKernelSet : ICoinKernelSet {
        private readonly INTMinerRoot _root;
        private readonly Dictionary<Guid, CoinKernelData> _dicById = new Dictionary<Guid, CoinKernelData>();

        public CoinKernelSet(INTMinerRoot root) {
            _root = root;
            _root.ServerContext.BuildCmdPath<AddCoinKernelCommand>("添加币种内核", LogEnum.DevConsole,
                action: (message) => {
                    InitOnece();
                    if (message == null || message.Input == null || message.Input.GetId() == Guid.Empty) {
                        throw new ArgumentNullException();
                    }
                    if (!_root.ServerContext.CoinSet.Contains(message.Input.CoinId)) {
                        throw new ValidationException("there is no coin with id" + message.Input.CoinId);
                    }
                    if (_dicById.ContainsKey(message.Input.GetId())) {
                        return;
                    }
                    if (_dicById.Values.Any(a => a.CoinId == message.Input.CoinId && a.KernelId == message.Input.KernelId)) {
                        return;
                    }
                    CoinKernelData entity = new CoinKernelData().Update(message.Input);
                    _dicById.Add(entity.Id, entity);
                    var repository = NTMinerRoot.CreateServerRepository<CoinKernelData>();
                    repository.Add(entity);

                    VirtualRoot.RaiseEvent(new CoinKernelAddedEvent(entity));

                    ICoin coin;
                    if (root.ServerContext.CoinSet.TryGetCoin(message.Input.CoinId, out coin)) {
                        IPool[] pools = root.ServerContext.PoolSet.Where(a => a.CoinId == coin.GetId()).ToArray();
                        foreach (IPool pool in pools) {
                            Guid poolKernelId = Guid.NewGuid();
                            var poolKernel = new PoolKernelData() {
                                Id = poolKernelId,
                                Args = string.Empty,
                                KernelId = message.Input.KernelId,
                                PoolId = pool.GetId()
                            };
                            VirtualRoot.Execute(new AddPoolKernelCommand(poolKernel));
                        }
                    }
                });
            _root.ServerContext.BuildCmdPath<UpdateCoinKernelCommand>("更新币种内核", LogEnum.DevConsole,
                action: (message) => {
                    InitOnece();
                    if (message == null || message.Input == null || message.Input.GetId() == Guid.Empty) {
                        throw new ArgumentNullException();
                    }
                    if (!_root.ServerContext.CoinSet.Contains(message.Input.CoinId)) {
                        throw new ValidationException("there is no coin with id" + message.Input.CoinId);
                    }
                    if (!_dicById.ContainsKey(message.Input.GetId())) {
                        return;
                    }
                    CoinKernelData entity = _dicById[message.Input.GetId()];
                    if (ReferenceEquals(entity, message.Input)) {
                        return;
                    }
                    entity.Update(message.Input);
                    var repository = NTMinerRoot.CreateServerRepository<CoinKernelData>();
                    repository.Update(entity);

                    VirtualRoot.RaiseEvent(new CoinKernelUpdatedEvent(entity));
                });
            _root.ServerContext.BuildCmdPath<RemoveCoinKernelCommand>("移除币种内核", LogEnum.DevConsole,
                action: (message) => {
                    InitOnece();
                    if (message == null || message.EntityId == Guid.Empty) {
                        throw new ArgumentNullException();
                    }
                    if (!_dicById.ContainsKey(message.EntityId)) {
                        return;
                    }
                    CoinKernelData entity = _dicById[message.EntityId];
                    _dicById.Remove(entity.Id);
                    var repository = NTMinerRoot.CreateServerRepository<CoinKernelData>();
                    repository.Remove(entity.Id);

                    VirtualRoot.RaiseEvent(new CoinKernelRemovedEvent(entity));
                    ICoin coin;
                    if (root.ServerContext.CoinSet.TryGetCoin(entity.CoinId, out coin)) {
                        List<Guid> toRemoves = new List<Guid>();
                        IPool[] pools = root.ServerContext.PoolSet.Where(a => a.CoinId == coin.GetId()).ToArray();
                        foreach (IPool pool in pools) {
                            foreach (PoolKernelData poolKernel in root.ServerContext.PoolKernelSet.Where(a => a.PoolId == pool.GetId() && a.KernelId == entity.KernelId)) {
                                toRemoves.Add(poolKernel.Id);
                            }
                        }
                        foreach (Guid poolKernelId in toRemoves) {
                            VirtualRoot.Execute(new RemovePoolKernelCommand(poolKernelId));
                        }
                    }
                });
            _root.ServerContext.BuildEventPath<FileWriterRemovedEvent>("移除文件书写器后移除引用关系", LogEnum.DevConsole,
                action: message => {
                    var repository = NTMinerRoot.CreateServerRepository<CoinKernelData>();
                    var entities = _dicById.Values.Where(a => a.FileWriterIds.Contains(message.Source.GetId())).ToArray();
                    foreach (var entity in entities) {
                        entity.FileWriterIds = new List<Guid>(entity.FileWriterIds.Where(a => a != message.Source.GetId()));
                        repository.Update(entity);
                        VirtualRoot.RaiseEvent(new CoinKernelUpdatedEvent(entity));
                    }
                });
            _root.ServerContext.BuildEventPath<FragmentWriterRemovedEvent>("移除命令行片段书写器后移除引用关系", LogEnum.DevConsole,
                action: message => {
                    var repository = NTMinerRoot.CreateServerRepository<CoinKernelData>();
                    var entities = _dicById.Values.Where(a => a.FragmentWriterIds.Contains(message.Source.GetId())).ToArray();
                    foreach (var entity in entities) {
                        entity.FragmentWriterIds = new List<Guid>(entity.FragmentWriterIds.Where(a => a != message.Source.GetId()));
                        repository.Update(entity);
                        VirtualRoot.RaiseEvent(new CoinKernelUpdatedEvent(entity));
                    }
                });
        }

        private bool _isInited = false;
        private readonly object _locker = new object();

        public int Count {
            get {
                InitOnece();
                return _dicById.Count;
            }
        }

        private void InitOnece() {
            if (_isInited) {
                return;
            }
            Init();
        }

        private void Init() {
            lock (_locker) {
                if (!_isInited) {
                    var repository = NTMinerRoot.CreateServerRepository<CoinKernelData>();
                    foreach (var item in repository.GetAll()) {
                        if (!_dicById.ContainsKey(item.GetId())) {
                            _dicById.Add(item.GetId(), item);
                        }
                    }
                    _isInited = true;
                }
            }
        }

        public bool Contains(Guid kernelId) {
            InitOnece();
            return _dicById.ContainsKey(kernelId);
        }

        public bool TryGetCoinKernel(Guid kernelId, out ICoinKernel kernel) {
            InitOnece();
            CoinKernelData k;
            var r = _dicById.TryGetValue(kernelId, out k);
            kernel = k;
            return r;
        }

        public IEnumerator<ICoinKernel> GetEnumerator() {
            InitOnece();
            return _dicById.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            InitOnece();
            return _dicById.Values.GetEnumerator();
        }
    }
}

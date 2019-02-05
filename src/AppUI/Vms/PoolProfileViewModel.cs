﻿using System;

namespace NTMiner.Vms {
    public class PoolProfileViewModel : ViewModelBase, IPoolProfile {
        private readonly IPoolProfile _inner;

        public PoolProfileViewModel(IPoolProfile innerProfile) {
            _inner = innerProfile;
        }

        public Guid PoolId {
            get { return _inner.PoolId; }
        }

        public string UserName {
            get => _inner.UserName;
            set {
                if (_inner.UserName != value) {
                    NTMinerRoot.Current.SetPoolProfileProperty(this.PoolId, nameof(UserName), value ?? string.Empty);
                    OnPropertyChanged(nameof(UserName));
                    Global.Execute(new RefreshArgsAssemblyCommand());
                }
            }
        }

        public string Password {
            get => _inner.Password;
            set {
                if (_inner.Password != value) {
                    NTMinerRoot.Current.SetPoolProfileProperty(this.PoolId, nameof(Password), value ?? string.Empty);
                    OnPropertyChanged(nameof(Password));
                    Global.Execute(new RefreshArgsAssemblyCommand());
                }
            }
        }
    }
}

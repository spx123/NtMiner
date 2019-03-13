﻿using System;
using System.Text;

namespace NTMiner.MinerServer {
    public class UpdateClientRequest : RequestBase, ISignatureRequest {
        public UpdateClientRequest() { }
        public string LoginName { get; set; }
        public string ObjectId { get; set; }
        public string PropertyName { get; set; }
        public object Value { get; set; }
        public string Sign { get; set; }

        public void SignIt(string password) {
            this.Sign = this.GetSign(password);
        }

        public string GetSign(string password) {
            StringBuilder sb = new StringBuilder();
            sb.Append(nameof(MessageId)).Append(MessageId)
                .Append(nameof(LoginName)).Append(LoginName)
                .Append(nameof(ObjectId)).Append(ObjectId)
                .Append(nameof(PropertyName)).Append(PropertyName)
                .Append(nameof(Value)).Append(Value)
                .Append(nameof(Timestamp)).Append(Timestamp.ToUlong())
                .Append(nameof(UserData.Password)).Append(password);
            return HashUtil.Sha1(sb.ToString());
        }
    }
}

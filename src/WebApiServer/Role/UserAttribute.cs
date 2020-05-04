﻿using NTMiner.Controllers;
using NTMiner.User;
using System;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;
using static NTMiner.Controllers.ApiControllerBase;

namespace NTMiner.Role {
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class UserAttribute : ActionFilterAttribute {
        protected virtual bool OnAuthorization(UserData user, out string message) {
            message = string.Empty;
            return true;
        }

        public override void OnActionExecuting(HttpActionContext actionContext) {
            base.OnActionExecuting(actionContext);
            var queryString = new NameValueCollection();
            string query = actionContext.Request.RequestUri.Query;
            if (!string.IsNullOrEmpty(query)) {
                query = query.Substring(1);
                string[] parts = query.Split('&');
                foreach (var item in parts) {
                    string[] pair = item.Split('=');
                    if (pair.Length == 2) {
                        queryString.Add(pair[0], pair[1]);
                    }
                }
            }
            long timestamp = 0;
            string t = queryString["timestamp"];
            if (!string.IsNullOrEmpty(t)) {
                long.TryParse(t, out timestamp);
            }
            ClientSignData clientSign = new ClientSignData(queryString["loginName"], queryString["sign"], timestamp);
            ISignableData data = null;
            var actionDescripter = actionContext.ActionDescriptor;
            var actionParameters = actionDescripter.GetParameters();
            bool isLoginAction = actionDescripter.ActionName == nameof(UserController.Login) 
                && actionDescripter.ControllerDescriptor.ControllerName == RpcRoot.GetControllerName<UserController>();
            if (actionParameters.Count == 1 && typeof(ISignableData).IsAssignableFrom(actionParameters[0].ParameterType)) {
                data = (ISignableData)actionContext.ActionArguments.First().Value;
            }
            string message = null;
            bool isValid = IsValidUser(clientSign, data, isLoginAction, out ResponseBase response, out UserData user);
            if (isValid) {
                isValid = OnAuthorization(user, out message);
            }
            if (!isValid) {
                if (response != null && !string.IsNullOrEmpty(message)) {
                    response.Description = message;
                }
                Type returnType = actionContext.ActionDescriptor.ReturnType;
                var httpResponseMessage = new HttpResponseMessage(HttpStatusCode.OK);
                if (returnType == typeof(HttpResponseMessage)) {
                    httpResponseMessage.Content = new ByteArrayContent(VirtualRoot.BinarySerializer.Serialize(response));
                    httpResponseMessage.Content.Headers.ContentType = new MediaTypeHeaderValue("image/jpg");
                }
                else {
                    httpResponseMessage.Content = new StringContent(VirtualRoot.JsonSerializer.Serialize(response), Encoding.UTF8, "application/json");
                }
                actionContext.Response = httpResponseMessage;
            }
            else {
                actionContext.ControllerContext.RouteData.Values["_user"] = user;
            }
        }

        private static bool IsValidUser(ClientSignData clientSign, ISignableData data, bool isLoginAction, out ResponseBase response, out UserData user) {
            user = null;
            if (!WebApiRoot.UserSet.IsReadied) {
                string message = "服务器用户集启动中，请稍后";
                response = ResponseBase.NotExist(message);
                return false;
            }
            if (!Timestamp.IsInTime(clientSign.Timestamp)) {
                response = ResponseBase.Expired();
                return false;
            }
            if (!string.IsNullOrEmpty(clientSign.LoginName)) {
                user = WebApiRoot.UserSet.GetUser(clientSign.UserId);
            }
            if (user == null) {
                string message = "用户不存在";
                response = ResponseBase.NotExist(message);
                return false;
            }
            if (isLoginAction) {
                if (!WebApiRoot.UserSet.CheckLoginTimes(clientSign.LoginName)) {
                    response = ResponseBase.Forbidden("对不起，您的尝试太过频繁");
                    return false;
                }
            }
            string mySign = RpcUser.CalcSign(user.LoginName, user.Password, clientSign.Timestamp, data);
            if (clientSign.Sign != mySign) {
                string message = "签名错误：1. 可能因为登录名或密码错误；2. 可能因为软件版本过期需要升级软件，请将软件升级到最新版本再试。";
                response = ResponseBase.Forbidden(message);
                return false;
            }
            response = null;
            return true;
        }
    }
}

﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using NewLife.Model;
using NewLife.Net.Tcp;
using NewLife.Net.Udp;

namespace NewLife.Net.Sockets
{
    /// <summary>网络服务器。可同时支持多个Socket服务器，同时支持IPv4和IPv6，同时支持Tcp和Udp</summary>
    /// <remarks>
    /// 网络服务器模型，所有网络应用服务器可以通过继承该类实现。
    /// 该类仅实现了业务应用对网络流的操作，与具体网络协议无关。
    /// 
    /// 快速用法：
    /// 指定端口后直接<see cref="Start"/>，NetServer将同时监听Tcp/Udp和IPv4/IPv6（会检查是否支持）四个端口。
    /// 
    /// 简单用法：
    /// 重载方法<see cref="EnsureCreateServer"/>来创建一个SocketServer并赋值给<see cref="Server"/>属性，<see cref="EnsureCreateServer"/>将会在<see cref="OnStart"/>时首先被调用。
    /// 
    /// 标准用法：
    /// 使用<see cref="AttachServer"/>方法向网络服务器添加Socket服务，其中第一个将作为默认Socket服务<see cref="Server"/>。
    /// 如果Socket服务集合<see cref="Servers"/>为空，将依据地址<see cref="Address"/>、端口<see cref="Port"/>、地址族<see cref="AddressFamily"/>、协议<see cref="ProtocolType"/>创建默认Socket服务。
    /// 如果地址族<see cref="AddressFamily"/>指定为IPv4和IPv6以外的值，将同时创建IPv4和IPv6两个Socket服务；
    /// 如果协议<see cref="ProtocolType"/>指定为Tcp和Udp以外的值，将同时创建Tcp和Udp两个Socket服务；
    /// 默认情况下，地址族<see cref="AddressFamily"/>和协议<see cref="ProtocolType"/>都是其它值，所以一共将会创建四个Socket服务（Tcp、Tcpv6、Udp、Udpv6）。
    /// </remarks>
    public class NetServer : Netbase, IServer
    {
        #region 属性
        private String _Name;
        /// <summary>服务名</summary>
        public String Name { get { return _Name ?? (_Name = GetType().Name); } set { _Name = value; } }

        private IPAddress _Address = IPAddress.Any;
        /// <summary>监听本地地址</summary>
        public IPAddress Address
        {
            get { return _Address; }
            set
            {
                _Address = value;
                if (value != null) AddressFamily = value.AddressFamily;
            }
        }

        private Int32 _Port;
        /// <summary>端口</summary>
        public Int32 Port { get { return _Port; } set { _Port = value; } }

        private AddressFamily _AddressFamily = AddressFamily.Unknown;
        /// <summary>地址族。如果使用Unknown，将同时使用IPv4和IPv6。</summary>
        public AddressFamily AddressFamily
        {
            get { return _AddressFamily; }
            set
            {
                _AddressFamily = value;

                // 根据地址族选择合适的本地地址
                _Address = _Address.GetRightAny(value);
            }
        }

        private List<ISocketServer> _Servers;
        /// <summary>服务器集合。</summary>
        protected IList<ISocketServer> Servers { get { return _Servers ?? (_Servers = new List<ISocketServer>()); } }

        //private SocketServer _Server;
        /// <summary>服务器。返回服务器集合中的第一个服务器</summary>
        public ISocketServer Server { get { return Servers.Count > 0 ? Servers[0] : null; } set { if (!Servers.Contains(value))_Servers.Insert(0, value); } }

        /// <summary>是否活动</summary>
        public Boolean Active { get { return Server != null && Server.Active; } }

        private ProtocolType _Protocol = ProtocolType.Unknown;
        /// <summary>协议类型。如果使用Unknown，将同时使用Tcp和Udp</summary>
        public ProtocolType ProtocolType { get { return _Protocol; } set { _Protocol = value; } }
        #endregion

        #region 构造
        /// <summary>实例化一个网络服务器</summary>
        public NetServer() { }

        /// <summary>通过指定监听地址和端口实例化一个网络服务器</summary>
        /// <param name="address"></param>
        /// <param name="port"></param>
        public NetServer(IPAddress address, Int32 port)
        {
            Address = address;
            Port = port;
        }

        /// <summary>通过指定监听地址和端口，还有协议，实例化一个网络服务器，默认支持Tcp协议和Udp协议</summary>
        /// <param name="address"></param>
        /// <param name="port"></param>
        /// <param name="protocolType"></param>
        public NetServer(IPAddress address, Int32 port, ProtocolType protocolType) : this(address, port) { ProtocolType = protocolType; }
        #endregion

        #region 创建
        /// <summary>添加Socket服务器</summary>
        /// <param name="server"></param>
        /// <returns>添加是否成功</returns>
        public virtual Boolean AttachServer(ISocketServer server)
        {
            if (!Servers.Contains(server))
            {
                Servers.Add(server);
                return true;
            }
            else
                return false;
        }

        /// <summary>确保建立服务器</summary>
        protected virtual void EnsureCreateServer()
        {
            if (Server == null)
            {
                var list = CreateServer(Address, Port, ProtocolType, AddressFamily);
                foreach (var item in list)
                {
                    AttachServer(item);
                }
            }
        }
        #endregion

        #region 方法
        /// <summary>开始服务</summary>
        public void Start()
        {
            if (Active) throw new InvalidOperationException("服务已经开始！");

            OnStart();

            ProtocolType = Server.ProtocolType;
        }

        /// <summary>开始时调用的方法</summary>
        protected virtual void OnStart()
        {
            EnsureCreateServer();

            foreach (var item in Servers)
            {
                item.Start();

                WriteLog("{0} 开始监听 {1}", Name, item);
            }
        }

        /// <summary>停止服务</summary>
        public void Stop()
        {
            if (!Active) throw new InvalidOperationException("服务没有开始！");

            foreach (var item in Servers)
            {
                WriteLog("{0} 停止监听 {1}", Name, item);
            }

            OnStop();
        }

        /// <summary>停止时调用的方法</summary>
        protected virtual void OnStop() { Dispose(); }

        /// <summary>子类重载实现资源释放逻辑</summary>
        /// <param name="disposing">从Dispose调用（释放所有资源）还是析构函数调用（释放非托管资源）</param>
        protected override void OnDispose(bool disposing)
        {
            base.OnDispose(disposing);

            // 释放托管资源
            //if (disposing)
            {
                //if (Server != null) Server.Stop();
                foreach (var item in Servers)
                {
                    item.Stop();
                }
            }
        }
        #endregion

        #region 创建Tcp/Udp、IPv4/IPv6服务
        /// <summary>创建Tcp/Udp、IPv4/IPv6服务</summary>
        /// <param name="address"></param>
        /// <param name="port"></param>
        /// <param name="protocol"></param>
        /// <param name="family"></param>
        /// <returns></returns>
        protected static ISocketServer[] CreateServer(IPAddress address, Int32 port, ProtocolType protocol, AddressFamily family)
        {
            if (protocol == ProtocolType.Tcp)
                return CreateServer<TcpServer>(address, port, family);
            else if (protocol == ProtocolType.Udp)
                return CreateServer<UdpServer>(address, port, family);
            else
            {
                var list = new List<ISocketServer>();

                // 其它未知协议，同时用Tcp和Udp
                list.AddRange(CreateServer<TcpServer>(address, port, family));
                list.AddRange(CreateServer<UdpServer>(address, port, family));

                return list.ToArray();
            }
        }

        static ISocketServer[] CreateServer<T>(IPAddress address, Int32 port, AddressFamily family) where T : ISocketServer, new()
        {
            var list = new List<ISocketServer>();
            switch (family)
            {
                case AddressFamily.InterNetwork:
                case AddressFamily.InterNetworkV6:
                    T svr = new T();
                    svr.Address = address.GetRightAny(family);
                    svr.Port = port;
                    svr.AddressFamily = family;

                    // 允许同时处理多个数据包
                    svr.NoDelay = svr.ProtocolType == ProtocolType.Udp;
                    svr.UseThreadPool = true;

                    if (!NetHelper.IsUsed(svr.ProtocolType, svr.Address, svr.Port)) list.Add(svr);
                    break;
                default:
                    // 其它情况表示同时支持IPv4和IPv6
                    if (Socket.SupportsIPv4) list.AddRange(CreateServer<T>(address, port, AddressFamily.InterNetwork));
                    if (Socket.OSSupportsIPv6) list.AddRange(CreateServer<T>(address, port, AddressFamily.InterNetworkV6));
                    break;
            }

            return list.ToArray();
        }
        #endregion

        #region 方法
        /// <summary>
        /// 已重载。
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            var servers = Servers;
            if (servers == null || servers.Count < 1) return base.ToString();

            if (servers.Count == 1) return servers[0].ToString();

            StringBuilder sb = new StringBuilder();
            foreach (var item in servers)
            {
                if (sb.Length > 0) sb.Append(" ");
                sb.Append(item);
            }
            return sb.ToString();
        }
        #endregion
    }
}
﻿using ENet;
using LeagueSandbox.GameServer.Logic.GameObjects;
using LeagueSandbox.GameServer.Logic.Packets;
using LeagueSandbox.GameServer.Logic.Players;

namespace LeagueSandbox.GameServer.Core.Logic.PacketHandlers.Packets
{
    class HandleStartGame : IPacketHandler
    {
        private Game _game = Program.ResolveDependency<Game>();
        private PlayerManager _playerManager = Program.ResolveDependency<PlayerManager>();

        public bool HandlePacket(Peer peer, byte[] data)
        {
            var peerInfo = _playerManager.GetPeerInfo(peer);

            if(!peerInfo.IsDisconnected)
            {
                _game.IncrementReadyPlayers();
            }

            /* if (_game.IsRunning)
                return true; */
            
            if (_game.PlayersReady == _playerManager.GetPlayers().Count)
            {
                var start = new StatePacket(PacketCmd.PKT_S2C_StartGame);
                _game.PacketHandlerManager.broadcastPacket(start, Channel.CHL_S2C);

                foreach (var player in _playerManager.GetPlayers())
                {
                    if (player.Item2.Peer == peer && !player.Item2.IsMatchingVersion)
                    {
                        var dm = new DebugMessage("Your client version does not match the server. Check the server log for more information.");
                        _game.PacketHandlerManager.sendPacket(peer, dm, Channel.CHL_S2C);
                    }
                    _game.PacketNotifier.NotifyUpdatedStats(player.Item2.Champion, false);
                }

                _game.Start();
            }

            if (_game.IsRunning)
            {
                var map = _game.Map;
                if (peerInfo.IsDisconnected)
                {
                    foreach (var player in _playerManager.GetPlayers())
                    {
                        if (player.Item2.Team == peerInfo.Team)
                        {
                            var heroSpawnPacket = new HeroSpawn2(player.Item2.Champion);
                            _game.PacketHandlerManager.sendPacket(peer, heroSpawnPacket, Channel.CHL_S2C);

                            /* This is probably not the best way
                             * of updating a champion's level, but it works */
                            var levelUpPacket = new LevelUp(player.Item2.Champion);
                            _game.PacketHandlerManager.sendPacket(peer, levelUpPacket, Channel.CHL_S2C);
                        }
                    }
                    peerInfo.IsDisconnected = false;
                    _game.PacketNotifier.NotifyUnitAnnounceEvent(UnitAnnounces.SummonerReconnected, peerInfo.Champion);

                    // Send the initial game time sync packets, then let the map send another
                    float gameTime = map.GameTime / 1000.0f;

                    var timer = new GameTimer(gameTime); // 0xC1
                    _game.PacketHandlerManager.sendPacket(peer, timer, Channel.CHL_S2C);

                    var timer2 = new GameTimerUpdate(gameTime); // 0xC2
                    _game.PacketHandlerManager.sendPacket(peer, timer2, Channel.CHL_S2C);

                    return true;
                }

                foreach (var p in _playerManager.GetPlayers())
                {
                    map.AddObject(p.Item2.Champion);

                    // Send the initial game time sync packets, then let the map send another
                    float gameTime = map.GameTime / 1000.0f;

                    var timer = new GameTimer(gameTime); // 0xC1
                    _game.PacketHandlerManager.sendPacket(p.Item2.Peer, timer, Channel.CHL_S2C);

                    var timer2 = new GameTimerUpdate(gameTime); // 0xC2
                    _game.PacketHandlerManager.sendPacket(p.Item2.Peer, timer2, Channel.CHL_S2C);
                }
            }

            return true;
        }
    }
}

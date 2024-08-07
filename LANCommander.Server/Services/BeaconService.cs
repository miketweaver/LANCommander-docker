﻿using BeaconLib;
using LANCommander.Server.Models;

namespace LANCommander.Server.Services
{
    public class BeaconService : IHostedService, IDisposable
    {
        private Beacon Beacon;
        private LANCommanderSettings Settings;

        public BeaconService() {
            Settings = SettingService.GetSettings();
            Beacon = new Beacon("LANCommander", Convert.ToUInt16(Settings.Port));
        }

        public void Dispose()
        {
            Beacon?.Dispose();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            Beacon.BeaconData = Settings.Beacon?.Address;
            Beacon.Start();

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            Beacon.Stop();
            return Task.CompletedTask;
        }
    }
}

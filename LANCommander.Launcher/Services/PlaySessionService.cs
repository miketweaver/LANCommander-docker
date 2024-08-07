﻿using JetBrains.Annotations;
using LANCommander.Launcher.Data;
using LANCommander.Launcher.Data.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.Launcher.Services
{
    public class PlaySessionService : BaseDatabaseService<PlaySession>
    {
        private readonly SDK.Client Client;

        public PlaySessionService(DatabaseContext dbContext, SDK.Client client) : base(dbContext)
        {
            Client = client;
        }

        public async Task<PlaySession> GetLatestSession(Guid gameId, Guid userId)
        {
            return await Get(ps => ps.GameId == gameId && ps.UserId == userId).OrderByDescending(ps => ps.End).FirstOrDefaultAsync();
        }

        public async Task StartSession(Guid gameId, Guid userId)
        {
            var existingSession = Get(ps => ps.GameId == gameId && ps.UserId == userId && ps.End == null).FirstOrDefault();

            if (existingSession != null)
                await Delete(existingSession);

            var session = new PlaySession()
            {
                GameId = gameId,
                UserId = userId,
                Start = DateTime.UtcNow
            };

            await Add(session);

            await Client.Games.StartPlaySessionAsync(gameId);
        }

        public async Task EndSession(Guid gameId, Guid userId)
        {
            var existingSession = Get(ps => ps.GameId == gameId && ps.UserId == userId && ps.End == null).FirstOrDefault();

            if (existingSession != null)
            {
                existingSession.End = DateTime.UtcNow;

                await Update(existingSession);
            }

            await Client.Games.EndPlaySessionAsync(gameId);
        }
    }
}

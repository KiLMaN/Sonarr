﻿using System;
using System.Collections.Generic;
using System.Linq;
using FizzWare.NBuilder;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.MediaFiles.Events;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.TvTests.EpisodeMonitoredServiceTests
{
    [TestFixture]
    public class SetEpisodeMontitoredFixture : CoreTest<EpisodeMonitoredService>
    {
        private Series _series;
        private List<Episode> _episodes;

        [SetUp]
        public void Setup()
        {
            var seasons = 4;

            _series = Builder<Series>.CreateNew()
                                     .With(s => s.Seasons = Builder<Season>.CreateListOfSize(seasons)
                                                                           .All()
                                                                           .With(n => n.Monitored = true)
                                                                           .Build()
                                                                           .ToList())
                                     .Build();

            _episodes = Builder<Episode>.CreateListOfSize(seasons)
                                        .All()
                                        .With(e => e.Monitored = true)
                                        .With(e => e.AirDateUtc = DateTime.UtcNow.AddDays(-7))
                                        //Missing
                                        .TheFirst(1)
                                        .With(e => e.EpisodeFileId = 0)
                                        //Has File
                                        .TheNext(1)
                                        .With(e => e.EpisodeFileId = 1)
                                         //Future
                                        .TheNext(1)
                                        .With(e => e.EpisodeFileId = 0)
                                        .With(e => e.AirDateUtc = DateTime.UtcNow.AddDays(7))
                                        //Future/TBA
                                        .TheNext(1)
                                        .With(e => e.EpisodeFileId = 0)
                                        .With(e => e.AirDateUtc = null)
                                        .Build()
                                        .ToList();

            Mocker.GetMock<IEpisodeService>()
                  .Setup(s => s.GetEpisodeBySeries(It.IsAny<int>()))
                  .Returns(_episodes);
        }

        private void GivenSpecials()
        {
            foreach (var episode in _episodes)
            {
                episode.SeasonNumber = 0;
            }

            _series.Seasons = new List<Season>{new Season { Monitored = false, SeasonNumber = 0 }};
        }

        [Test]
        public void should_be_able_to_monitor_all_episodes()
        {
            Subject.SetEpisodeMonitoredStatus(_series, new MonitoringOptions());

            Mocker.GetMock<IEpisodeService>()
                  .Verify(v => v.UpdateEpisodes(It.Is<List<Episode>>(l => l.All(e => e.Monitored))));
        }

        [Test]
        public void should_be_able_to_monitor_missing_episodes_only()
        {
            var monitoringOptions = new MonitoringOptions
                                    {
                                        IgnoreEpisodesWithFiles = true,
                                        IgnoreEpisodesWithoutFiles = false
                                    };

            Subject.SetEpisodeMonitoredStatus(_series, monitoringOptions);

            VerifyMonitored(e => !e.HasFile);
            VerifyNotMonitored(e => e.HasFile);
        }

        [Test]
        public void should_be_able_to_monitor_new_episodes_only()
        {
            var monitoringOptions = new MonitoringOptions
            {
                IgnoreEpisodesWithFiles = true,
                IgnoreEpisodesWithoutFiles = true
            };

            Subject.SetEpisodeMonitoredStatus(_series, monitoringOptions);

            VerifyMonitored(e => e.AirDateUtc.HasValue && e.AirDateUtc.Value.After(DateTime.UtcNow));
            VerifyMonitored(e => !e.AirDateUtc.HasValue);
            VerifyNotMonitored(e => e.AirDateUtc.HasValue && e.AirDateUtc.Value.Before(DateTime.UtcNow));
        }

        [Test]
        public void should_not_monitor_missing_specials()
        {
            GivenSpecials();

            var monitoringOptions = new MonitoringOptions
            {
                IgnoreEpisodesWithFiles = true,
                IgnoreEpisodesWithoutFiles = false
            };

            Subject.SetEpisodeMonitoredStatus(_series, monitoringOptions);

            VerifyNotMonitored(e => e.SeasonNumber == 0);
        }

        [Test]
        public void should_not_monitor_new_specials()
        {
            GivenSpecials();

            var monitoringOptions = new MonitoringOptions
            {
                IgnoreEpisodesWithFiles = true,
                IgnoreEpisodesWithoutFiles = true
            };

            Subject.SetEpisodeMonitoredStatus(_series, monitoringOptions);

            VerifyNotMonitored(e => e.SeasonNumber == 0);
        }

        [Test]
        public void should_not_monitor_season_when_all_episodes_are_monitored_except_latest_season()
        {
            _series.Seasons = Builder<Season>.CreateListOfSize(2)
                                             .All()
                                             .With(n => n.Monitored = true)
                                             .Build()
                                             .ToList();

            _episodes = Builder<Episode>.CreateListOfSize(5)
                                        .All()
                                        .With(e => e.SeasonNumber = 1)
                                        .With(e => e.EpisodeFileId = 0)
                                        .With(e => e.AirDateUtc = DateTime.UtcNow.AddDays(-5))
                                        .TheLast(1)
                                        .With(e => e.SeasonNumber = 2)
                                        .Build()
                                        .ToList();
            
            Mocker.GetMock<IEpisodeService>()
                  .Setup(s => s.GetEpisodeBySeries(It.IsAny<int>()))
                  .Returns(_episodes);

            var monitoringOptions = new MonitoringOptions
            {
                IgnoreEpisodesWithoutFiles = true
            };

            Subject.SetEpisodeMonitoredStatus(_series, monitoringOptions);

            VerifySeasonMonitored(n => n.SeasonNumber == 2);
            VerifySeasonNotMonitored(n => n.SeasonNumber == 1);
        }

        [Test]
        public void should_ignore_episodes_when_season_is_not_monitored()
        {
            _series.Seasons.ForEach(s => s.Monitored = false);

            Subject.SetEpisodeMonitoredStatus(_series, new MonitoringOptions());

            Mocker.GetMock<IEpisodeService>()
                  .Verify(v => v.UpdateEpisodes(It.Is<List<Episode>>(l => l.All(e => !e.Monitored))));
        }

        private void VerifyMonitored(Func<Episode, bool> predicate)
        {
            Mocker.GetMock<IEpisodeService>()
                .Verify(v => v.UpdateEpisodes(It.Is<List<Episode>>(l => l.Where(predicate).All(e => e.Monitored))));
        }

        private void VerifyNotMonitored(Func<Episode, bool> predicate)
        {
            Mocker.GetMock<IEpisodeService>()
                .Verify(v => v.UpdateEpisodes(It.Is<List<Episode>>(l => l.Where(predicate).All(e => !e.Monitored))));
        }

        private void VerifySeasonMonitored(Func<Season, bool> predicate)
        {
            Mocker.GetMock<ISeriesService>()
                  .Verify(v => v.UpdateSeries(It.Is<Series>(s => s.Seasons.Where(predicate).All(n => n.Monitored))));
        }

        private void VerifySeasonNotMonitored(Func<Season, bool> predicate)
        {
            Mocker.GetMock<ISeriesService>()
                  .Verify(v => v.UpdateSeries(It.Is<Series>(s => s.Seasons.Where(predicate).All(n => !n.Monitored))));
        }
    }
}

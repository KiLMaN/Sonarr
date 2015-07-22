﻿using System;
using System.Linq;
using System.Net;
using FluentValidation.Results;
using NLog;
using RestSharp;
using NzbDrone.Core.Rest;

namespace NzbDrone.Core.Notifications.PushBullet
{
    public interface IPushBulletProxy
    {
        void SendNotification(string title, string message, PushBulletSettings settings);
        ValidationFailure Test(PushBulletSettings settings);
    }

    public class PushBulletProxy : IPushBulletProxy
    {
        private readonly Logger _logger;
        private const string URL = "https://api.pushbullet.com/v2/pushes";

        public PushBulletProxy(Logger logger)
        {
            _logger = logger;
        }

        public void SendNotification(string title, string message, PushBulletSettings settings)
        {
            var error = false;

            if (settings.ChannelTags.Any())
            {
                foreach (var channelTag in settings.ChannelTags)
                {
                    var request = BuildChannelRequest(channelTag);

                    try
                    {
                        SendNotification(title, message, request, settings);
                    }
                    catch (PushBulletException ex)
                    {
                        _logger.ErrorException("Unable to send test message to: " + channelTag, ex);
                        error = true;
                    }
                }
            }
            else
            {
                if (settings.DeviceIds.Any())
                {
                    foreach (var deviceId in settings.DeviceIds)
                    {
                        var request = BuildDeviceRequest(deviceId);

                        try
                        {
                            SendNotification(title, message, request, settings);
                        }
                        catch (PushBulletException ex)
                        {
                            _logger.ErrorException("Unable to send test message to: " + deviceId, ex);
                            error = true;
                        }
                    }
                }
                else
                {
                    var request = BuildDeviceRequest(null);

                    try
                    {
                        SendNotification(title, message, request, settings);
                    }
                    catch (PushBulletException ex)
                    {
                        _logger.ErrorException("Unable to send test message to all devices", ex);
                        error = true;
                    }
                }
            }

            if (error)
            {
                throw new PushBulletException("Unable to send PushBullet notifications to all channels or devices");
            }
        }

        public ValidationFailure Test(PushBulletSettings settings)
        {
            try
            {
                const string title = "Sonarr - Test Notification";
                const string body = "This is a test message from Sonarr";

                SendNotification(title, body, settings);
            }
            catch (RestException ex)
            {
                if (ex.Response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    _logger.ErrorException("API Key is invalid: " + ex.Message, ex);
                    return new ValidationFailure("ApiKey", "API Key is invalid");
                }

                _logger.ErrorException("Unable to send test message: " + ex.Message, ex);
                return new ValidationFailure("ApiKey", "Unable to send test message");
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Unable to send test message: " + ex.Message, ex);
                return new ValidationFailure("", "Unable to send test message");
            }

            return null;
        }

        private RestRequest BuildDeviceRequest(string deviceId)
        {
            var request = new RestRequest(Method.POST);
            long integerId;

            if (Int64.TryParse(deviceId, out integerId))
            {
                request.AddParameter("device_id", integerId);
            }

            else
            {
                request.AddParameter("device_iden", deviceId);
            }

            return request;
        }

        private RestRequest BuildChannelRequest(string channelTag)
        {
            var request = new RestRequest(Method.POST);
            request.AddParameter("channel_tag", channelTag);

            return request;
        }

        private void SendNotification(string title, string message, RestRequest request, PushBulletSettings settings)
        {
            try
            {
                var client = RestClientFactory.BuildClient(URL);

                request.AddParameter("type", "note");
                request.AddParameter("title", title);
                request.AddParameter("body", message);

                client.Authenticator = new HttpBasicAuthenticator(settings.ApiKey, String.Empty);
                client.ExecuteAndValidate(request);
            }
            catch (RestException ex)
            {
                if (ex.Response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    _logger.ErrorException("API Key is invalid: " + ex.Message, ex);
                    throw;
                }

                throw new PushBulletException("Unable to send text message: {0}", ex, ex.Message);
            }
        }
    }
}

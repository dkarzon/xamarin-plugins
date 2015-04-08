﻿using Android.App;
using Android.Content;
using Android.Media;
using Android.OS;
using Android.Support.V4.App;
using Geofence.Plugin.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Geofence.Plugin
{
    [Service]
    public class GeofenceTransitionsIntentService : IntentService
    {
        public static int NotificationId = 0;
        public const int  NotificationMaxId = 6;
        protected async override void OnHandleIntent(Intent intent)
        {
            Context context = Android.App.Application.Context;
            Bundle extras = intent.Extras;
            Android.Gms.Location.GeofencingEvent geofencingEvent = Android.Gms.Location.GeofencingEvent.FromIntent(intent);

            if (geofencingEvent.HasError)
            {
                string errorMessage = Android.Gms.Location.GeofenceStatusCodes.GetStatusCodeString(geofencingEvent.ErrorCode);
                string message = string.Format("{0} - {1}", CrossGeofence.Id, errorMessage);
                System.Diagnostics.Debug.WriteLine(message);
                CrossGeofence.GeofenceListener.OnError(message);
            }
            // Get the transition type.
            int geofenceTransition = geofencingEvent.GeofenceTransition;
            // Get the geofences that were triggered. A single event can trigger
            // multiple geofences.
            IList<Android.Gms.Location.IGeofence> triggeringGeofences = geofencingEvent.TriggeringGeofences;


            // List<string> geofenceIds = new List<string>();
            GeofenceTransition gTransition = GeofenceTransition.Unknown;

            ((GeofenceImplementation)CrossGeofence.Current).CurrentRequestType = Geofence.Plugin.GeofenceImplementation.RequestType.Update;

            foreach (Android.Gms.Location.IGeofence geofence in triggeringGeofences)
            {

                if (!CrossGeofence.Current.GeofenceResults.ContainsKey(geofence.RequestId))
                {
                    ((GeofenceImplementation)CrossGeofence.Current).AddGeofenceResult(geofence.RequestId);

                }
                //geofencingEvent.TriggeringLocation.Accuracy
                CrossGeofence.Current.GeofenceResults[geofence.RequestId].Latitude = geofencingEvent.TriggeringLocation.Latitude;
                CrossGeofence.Current.GeofenceResults[geofence.RequestId].Longitude = geofencingEvent.TriggeringLocation.Longitude;
                CrossGeofence.Current.GeofenceResults[geofence.RequestId].Accuracy = geofencingEvent.TriggeringLocation.Accuracy;

                double seconds = geofencingEvent.TriggeringLocation.Time / 1000;
                DateTime resultDate = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Local).AddSeconds(seconds);

                //DateTime resultDate = DateTime.Now;

                switch (geofenceTransition)
                {
                    case Android.Gms.Location.Geofence.GeofenceTransitionEnter:
                        gTransition = GeofenceTransition.Entered;
                        CrossGeofence.Current.GeofenceResults[geofence.RequestId].LastEnterTime = resultDate;
                        CrossGeofence.Current.GeofenceResults[geofence.RequestId].LastExitTime = null;
                        break;
                    case Android.Gms.Location.Geofence.GeofenceTransitionExit:
                        gTransition = GeofenceTransition.Exited;
                        CrossGeofence.Current.GeofenceResults[geofence.RequestId].LastExitTime = resultDate;
                        break;
                    case Android.Gms.Location.Geofence.GeofenceTransitionDwell:
                        gTransition = GeofenceTransition.Stayed;
                        break;
                    default:
                        string message = string.Format("{0} - {1}", CrossGeofence.Id, "Invalid transition type");
                        System.Diagnostics.Debug.WriteLine(message);
                        gTransition = GeofenceTransition.Unknown;
                        break;
                }

                if (CrossGeofence.Current.GeofenceResults[geofence.RequestId].Transition != gTransition )
                {
                    CrossGeofence.Current.GeofenceResults[geofence.RequestId].Transition = gTransition;

                    if (CrossGeofence.EnableLocalNotifications)
                    {
                       string message=string.Format("{0} {1} {2}", GeofenceResult.GetTransitionString(CrossGeofence.Current.GeofenceResults[geofence.RequestId].Transition), "geofence region:", geofence.RequestId);
                       
                        if(CrossGeofence.Current.Regions.ContainsKey(geofence.RequestId))
                       {
                          switch(gTransition)
                         {
                           case GeofenceTransition.Entered:
                               message=string.IsNullOrEmpty(CrossGeofence.Current.Regions[geofence.RequestId].EntryMessage)?message:CrossGeofence.Current.Regions[geofence.RequestId].EntryMessage;
                               break;
                           case GeofenceTransition.Exited:
                               message=string.IsNullOrEmpty(CrossGeofence.Current.Regions[geofence.RequestId].ExitMessage)?message:CrossGeofence.Current.Regions[geofence.RequestId].ExitMessage;
                               break;
                           case GeofenceTransition.Stayed:
                               message=string.IsNullOrEmpty(CrossGeofence.Current.Regions[geofence.RequestId].StayMessage)?message:CrossGeofence.Current.Regions[geofence.RequestId].StayMessage;
                               break;

                         }
                       }
                     

                       CreateNotification(context.ApplicationInfo.LoadLabel(context.PackageManager), message);
                    }
                  
                    CrossGeofence.GeofenceListener.OnRegionStateChanged(CrossGeofence.Current.GeofenceResults[geofence.RequestId]);


                    if (CrossGeofence.Current.GeofenceResults.ContainsKey(geofence.RequestId) && CrossGeofence.Current.Regions.ContainsKey(geofence.RequestId) && CrossGeofence.Current.Regions[geofence.RequestId].NotifyOnStay && CrossGeofence.Current.GeofenceResults[geofence.RequestId].Transition == GeofenceTransition.Entered && CrossGeofence.StayedInDuration != 0)
                    {
                        await Task.Delay(CrossGeofence.StayedInDuration);

                        if (CrossGeofence.Current.GeofenceResults[geofence.RequestId].LastExitTime == null && CrossGeofence.Current.GeofenceResults[geofence.RequestId].Transition != GeofenceTransition.Stayed)
                        {
                            CrossGeofence.Current.GeofenceResults[geofence.RequestId].Transition = GeofenceTransition.Stayed;

                            CrossGeofence.GeofenceListener.OnRegionStateChanged(CrossGeofence.Current.GeofenceResults[geofence.RequestId]);

                            if (CrossGeofence.EnableLocalNotifications)
                            {
                                CreateNotification(context.ApplicationInfo.LoadLabel(context.PackageManager), string.IsNullOrEmpty(CrossGeofence.Current.Regions[geofence.RequestId].StayMessage) ? string.Format("{0} {1} {2}", GeofenceResult.GetTransitionString(CrossGeofence.Current.GeofenceResults[geofence.RequestId].Transition), "geofence region:", geofence.RequestId) : CrossGeofence.Current.Regions[geofence.RequestId].StayMessage);
                            }
                    
                        }
                    }
                   
                   
                }


            }


           
        }
       

        public static void CreateNotification(string title, string message)
        {
           
            try
            {

                NotificationCompat.Builder builder = null;
                Context context = Android.App.Application.Context;

                if (CrossGeofence.SoundUri == null)
                {
                    CrossGeofence.SoundUri = RingtoneManager.GetDefaultUri(RingtoneType.Notification);
                }
                try
                {

                    if (CrossGeofence.IconResource == 0)
                    {
                        CrossGeofence.IconResource = context.ApplicationInfo.Icon;
                    }
                    else
                    {
                        string name = context.Resources.GetResourceName(CrossGeofence.IconResource);

                        if (name == null)
                        {
                            CrossGeofence.IconResource = context.ApplicationInfo.Icon;

                        }
                    }

                }
                catch (Android.Content.Res.Resources.NotFoundException ex)
                {
                    CrossGeofence.IconResource = context.ApplicationInfo.Icon;
                    System.Diagnostics.Debug.WriteLine(ex.ToString());
                }

                Intent resultIntent = context.PackageManager.GetLaunchIntentForPackage(context.PackageName);

                // Create a PendingIntent; we're only using one PendingIntent (ID = 0):
                const int pendingIntentId = 0;
                PendingIntent resultPendingIntent = PendingIntent.GetActivity(context, pendingIntentId, resultIntent, PendingIntentFlags.OneShot);


                // Build the notification
                builder = new NotificationCompat.Builder(context)
                        .SetAutoCancel(true) // dismiss the notification from the notification area when the user clicks on it
                        .SetContentIntent(resultPendingIntent) // start up this activity when the user clicks the intent.
                        .SetContentTitle(title) // Set the title
                        .SetSound(CrossGeofence.SoundUri)
                        .SetSmallIcon(CrossGeofence.IconResource) // This is the icon to display
                        .SetContentText(message); // the message to display.


                NotificationManager notificationManager = (NotificationManager)context.GetSystemService(Context.NotificationService);

                if (NotificationId >= NotificationMaxId)
                {
                    NotificationId = 0;
                }

                notificationManager.Notify(NotificationId++, builder.Build());

            }
            catch (Java.Lang.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("{0} - {1}", CrossGeofence.Id, ex.ToString()));
            }
            catch (System.Exception ex1)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("{0} - {1}", CrossGeofence.Id, ex1.ToString()));
            }
           
        }
    }
}

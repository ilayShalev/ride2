using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GMap.NET;

namespace RideMatchProject.Utilities
{
    public static class PolylineEncoder
    {
        /// <summary>
        /// Decodes a Google Maps encoded polyline string into a list of points
        /// </summary>
        public static List<PointLatLng> Decode(string encodedPoints)
        {
            if (string.IsNullOrEmpty(encodedPoints))
                return new List<PointLatLng>();

            var polyline = new List<PointLatLng>();
            int index = 0;
            int len = encodedPoints.Length;
            int lat = 0;
            int lng = 0;

            while (index < len)
            {
                int b;
                int shift = 0;
                int result = 0;

                do
                {
                    b = encodedPoints[index++] - 63;
                    result |= (b & 0x1f) << shift;
                    shift += 5;
                } while (b >= 0x20);

                int dlat = ((result & 1) != 0 ? ~(result >> 1) : (result >> 1));
                lat += dlat;

                shift = 0;
                result = 0;

                do
                {
                    b = encodedPoints[index++] - 63;
                    result |= (b & 0x1f) << shift;
                    shift += 5;
                } while (b >= 0x20);

                int dlng = ((result & 1) != 0 ? ~(result >> 1) : (result >> 1));
                lng += dlng;

                polyline.Add(new PointLatLng(lat / 1E5, lng / 1E5));
            }

            return polyline;
        }

        /// <summary>
        /// Encodes a list of points into a Google Maps encoded polyline string
        /// </summary>
        public static string Encode(List<PointLatLng> points)
        {
            var result = new System.Text.StringBuilder();

            int prevLat = 0;
            int prevLng = 0;

            foreach (var point in points)
            {
                int lat = (int)Math.Round(point.Lat * 1E5);
                int lng = (int)Math.Round(point.Lng * 1E5);

                // Encode latitude
                EncodeValue(result, lat - prevLat);
                prevLat = lat;

                // Encode longitude
                EncodeValue(result, lng - prevLng);
                prevLng = lng;
            }

            return result.ToString();
        }

        private static void EncodeValue(System.Text.StringBuilder result, int value)
        {
            value = value < 0 ? ~(value << 1) : (value << 1);

            while (value >= 0x20)
            {
                result.Append((char)((0x20 | (value & 0x1f)) + 63));
                value >>= 5;
            }

            result.Append((char)(value + 63));
        }
    }
}

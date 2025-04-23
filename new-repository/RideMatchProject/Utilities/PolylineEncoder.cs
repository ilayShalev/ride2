using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GMap.NET;

namespace RideMatchProject.Utilities
{
    /// <summary>
    /// Facade for polyline encoding/decoding operations
    /// </summary>
    public static class PolylineEncoder
    {
        /// <summary>
        /// Decodes a Google Maps encoded polyline string into a list of points
        /// </summary>
        public static List<PointLatLng> Decode(string encodedPoints)
        {
            var decoder = new PolylineDecoder();
            return decoder.DecodePolyline(encodedPoints);
        }

        /// <summary>
        /// Encodes a list of points into a Google Maps encoded polyline string
        /// </summary>
        public static string Encode(List<PointLatLng> points)
        {
            var encoder = new PolylineEncoderImplementation();
            return encoder.EncodePolyline(points);
        }
    }

    /// <summary>
    /// Handles decoding of polyline strings
    /// </summary>
    internal class PolylineDecoder
    {
        public List<PointLatLng> DecodePolyline(string encodedPoints)
        {
            if (string.IsNullOrEmpty(encodedPoints))
            {
                return new List<PointLatLng>();
            }

            var polyline = new List<PointLatLng>();
            int index = 0;
            int lat = 0;
            int lng = 0;

            while (index < encodedPoints.Length)
            {
                int dlat = DecodeNextValue(encodedPoints, ref index);
                int dlng = DecodeNextValue(encodedPoints, ref index);

                lat += dlat;
                lng += dlng;

                polyline.Add(new PointLatLng(lat / 1E5, lng / 1E5));
            }

            return polyline;
        }

        private int DecodeNextValue(string encodedPoints, ref int index)
        {
            int shift = 0;
            int result = 0;
            int b;

            do
            {
                b = encodedPoints[index++] - 63;
                result |= (b & 0x1f) << shift;
                shift += 5;
            } while (b >= 0x20);

            return ((result & 1) != 0 ? ~(result >> 1) : (result >> 1));
        }
    }

    /// <summary>
    /// Handles encoding of polyline coordinates
    /// </summary>
    internal class PolylineEncoderImplementation
    {
        public string EncodePolyline(List<PointLatLng> points)
        {
            if (points == null || points.Count == 0)
            {
                return string.Empty;
            }

            var result = new StringBuilder();
            int prevLat = 0;
            int prevLng = 0;

            foreach (var point in points)
            {
                EncodeCoordinates(result, point, ref prevLat, ref prevLng);
            }

            return result.ToString();
        }

        private void EncodeCoordinates(StringBuilder result, PointLatLng point,
            ref int prevLat, ref int prevLng)
        {
            int lat = (int)Math.Round(point.Lat * 1E5);
            int lng = (int)Math.Round(point.Lng * 1E5);

            EncodeValue(result, lat - prevLat);
            prevLat = lat;

            EncodeValue(result, lng - prevLng);
            prevLng = lng;
        }

        private void EncodeValue(StringBuilder result, int value)
        {
            int transformedValue = ConvertValueForEncoding(value);
            AppendEncodedValue(result, transformedValue);
        }

        private int ConvertValueForEncoding(int value)
        {
            return value < 0 ? ~(value << 1) : (value << 1);
        }

        private void AppendEncodedValue(StringBuilder result, int value)
        {
            while (value >= 0x20)
            {
                result.Append((char)((0x20 | (value & 0x1f)) + 63));
                value >>= 5;
            }

            result.Append((char)(value + 63));
        }
    }
}
﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LeanCloud.Storage.Internal {
    internal class QCloudUploader : IFileUploader {
        private object mutex = new object();

        FileState fileState;
        Stream data;
        string bucket;
        string token;
        string uploadUrl;
        bool done;
        private long sliceSize = (long)CommonSize.KB512;

        public async Task<FileState> Upload(FileState state, Stream dataStream, IDictionary<string, object> fileToken, IProgress<AVUploadProgressEventArgs> progress, CancellationToken cancellationToken) {
            fileState = state;
            data = dataStream;
            uploadUrl = fileToken["upload_url"].ToString();
            token = fileToken["token"].ToString();
            fileState.ObjectId = fileToken["objectId"].ToString();
            bucket = fileToken["bucket"].ToString();
            var result = await FileSlice(cancellationToken);
            if (done) {
                return state;
            }
            var response = result.Item2;
            var resumeData = response["data"] as IDictionary<string, object>;
            if (resumeData.ContainsKey("access_url")) {
                return state;
            }
            var sliceSession = resumeData["session"].ToString();
            var sliceOffset = long.Parse(resumeData["offset"].ToString());
            return await UploadSlice(sliceSession, sliceOffset, dataStream, progress, cancellationToken);
        }

        async Task<FileState> UploadSlice(
            string sessionId,
            long offset,
            Stream dataStream,
            IProgress<AVUploadProgressEventArgs> progress,
            CancellationToken cancellationToken) {

            long dataLength = dataStream.Length;
            if (progress != null) {
                lock (mutex) {
                    progress.Report(new AVUploadProgressEventArgs() {
                        Progress = AVFileController.CalcProgress(offset, dataLength)
                    });
                }
            }

            if (offset == dataLength) {
                return fileState;
            }

            var sliceFile = GetNextBinary(offset, dataStream);
            var result = await ExcuteUpload(sessionId, offset, sliceFile, cancellationToken);
            offset += sliceFile.Length;
            if (offset == dataLength) {
                done = true;
                return fileState;
            }
            var response = result.Item2;
            var resumeData = response["data"] as IDictionary<string, object>;
            var sliceSession = resumeData["session"].ToString();
            return await UploadSlice(sliceSession, offset, dataStream, progress, cancellationToken);
        }

        Task<Tuple<HttpStatusCode, IDictionary<string, object>>> ExcuteUpload(string sessionId, long offset, byte[] sliceFile, CancellationToken cancellationToken) {
            var body = new Dictionary<string, object>();
            body.Add("op", "upload_slice");
            body.Add("session", sessionId);
            body.Add("offset", offset.ToString());

            return PostToQCloud(body, sliceFile, cancellationToken);
        }

        async Task<Tuple<HttpStatusCode, IDictionary<string, object>>> FileSlice(CancellationToken cancellationToken) {
            SHA1CryptoServiceProvider sha1 = new SHA1CryptoServiceProvider();
            var body = new Dictionary<string, object>();
            if (data.Length <= (long)CommonSize.KB512) {
                body.Add("op", "upload");
                body.Add("sha", HexStringFromBytes(sha1.ComputeHash(data)));
                var wholeFile = GetNextBinary(0, data);
                var result = await PostToQCloud(body, wholeFile, cancellationToken);
                if (result.Item1 == HttpStatusCode.OK) {
                    done = true;
                }
                return result;
            } else {
                body.Add("op", "upload_slice");
                body.Add("filesize", data.Length);
                body.Add("sha", HexStringFromBytes(sha1.ComputeHash(data)));
                body.Add("slice_size", (long)CommonSize.KB512);
            }

            return await PostToQCloud(body, null, cancellationToken);
        }
        public static string HexStringFromBytes(byte[] bytes) {
            var sb = new StringBuilder();
            foreach (byte b in bytes) {
                var hex = b.ToString("x2");
                sb.Append(hex);
            }
            return sb.ToString();
        }

        public static string SHA1HashStringForUTF8String(string s) {
            byte[] bytes = Encoding.UTF8.GetBytes(s);

            SHA1CryptoServiceProvider sha1 = new SHA1CryptoServiceProvider();
            byte[] hashBytes = sha1.ComputeHash(bytes);

            return HexStringFromBytes(hashBytes);
        }

        async Task<Tuple<HttpStatusCode, IDictionary<string, object>>> PostToQCloud(
            Dictionary<string, object> body,
            byte[] sliceFile,
            CancellationToken cancellationToken) {
            IList<KeyValuePair<string, string>> sliceHeaders = new List<KeyValuePair<string, string>>();
            sliceHeaders.Add(new KeyValuePair<string, string>("Authorization", this.token));

            string contentType;
            long contentLength;

            var tempStream = HttpUploadFile(sliceFile, fileState.CloudName, out contentType, out contentLength, body);

            sliceHeaders.Add(new KeyValuePair<string, string>("Content-Type", contentType));

            var client = new HttpClient();
            var request = new HttpRequestMessage {
                RequestUri = new Uri(uploadUrl),
                Method = HttpMethod.Post,
                Content = new StreamContent(tempStream)
            };
            foreach (var header in sliceHeaders) {
                request.Headers.Add(header.Key, header.Value);
            }
            var response = await client.SendAsync(request);
            client.Dispose();
            request.Dispose();
            var content = await response.Content.ReadAsStringAsync();
            response.Dispose();
            // TODO 修改反序列化返回
            return await JsonUtils.DeserializeObjectAsync<Tuple<HttpStatusCode, IDictionary<string, object>>>(content);
        }
        public static Stream HttpUploadFile(byte[] file, string fileName, out string contentType, out long contentLength, IDictionary<string, object> nvc) {
            string boundary = "---------------------------" + DateTime.Now.Ticks.ToString("x");
            byte[] boundarybytes = StringToAscii("\r\n--" + boundary + "\r\n");
            contentType = "multipart/form-data; boundary=" + boundary;

            MemoryStream rs = new MemoryStream();

            string formdataTemplate = "Content-Disposition: form-data; name=\"{0}\"\r\n\r\n{1}";
            foreach (string key in nvc.Keys) {
                rs.Write(boundarybytes, 0, boundarybytes.Length);
                string formitem = string.Format(formdataTemplate, key, nvc[key]);
                byte[] formitembytes = System.Text.Encoding.UTF8.GetBytes(formitem);
                rs.Write(formitembytes, 0, formitembytes.Length);
            }
            rs.Write(boundarybytes, 0, boundarybytes.Length);

            if (file != null) {
                string headerTemplate = "Content-Disposition: form-data; name=\"{0}\"; filename=\"{1}\"\r\nContent-Type: {2}\r\n\r\n";
                string header = string.Format(headerTemplate, "fileContent", fileName, "application/octet-stream");
                byte[] headerbytes = System.Text.Encoding.UTF8.GetBytes(header);
                rs.Write(headerbytes, 0, headerbytes.Length);

                rs.Write(file, 0, file.Length);
            }

            byte[] trailer = StringToAscii("\r\n--" + boundary + "--\r\n");
            rs.Write(trailer, 0, trailer.Length);
            contentLength = rs.Length;

            rs.Position = 0;
            var tempBuffer = new byte[rs.Length];
            rs.Read(tempBuffer, 0, tempBuffer.Length);

            return new MemoryStream(tempBuffer);
        }

        public static byte[] StringToAscii(string s) {
            byte[] retval = new byte[s.Length];
            for (int ix = 0; ix < s.Length; ++ix) {
                char ch = s[ix];
                if (ch <= 0x7f) retval[ix] = (byte)ch;
                else retval[ix] = (byte)'?';
            }
            return retval;
        }

        byte[] GetNextBinary(long completed, Stream dataStream) {
            if (completed + sliceSize > dataStream.Length) {
                sliceSize = dataStream.Length - completed;
            }

            byte[] chunkBinary = new byte[sliceSize];
            dataStream.Seek(completed, SeekOrigin.Begin);
            dataStream.Read(chunkBinary, 0, (int)sliceSize);
            return chunkBinary;
        }
    }
}

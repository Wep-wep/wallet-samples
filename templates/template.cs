﻿/*
 * Copyright 2022 Google Inc.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

// [START setup]
using Google.Apis.Auth.OAuth2;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;

// Path to service account key file obtained from Google CLoud Console.
var serviceAccountJson = Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS") ?? "/path/to/key.json";

// Issuer ID obtained from Google Pay Business Console.
var issuerId = Environment.GetEnvironmentVariable("WALLET_ISSUER_ID") ?? "<issuer ID>";

// Developer defined ID for the wallet class.
var classId = Environment.GetEnvironmentVariable("WALLET_CLASS_ID") ?? "test-$object_type-class-id";

// Developer defined ID for the user, eg an email address.
var userId = Environment.GetEnvironmentVariable("WALLET_USER_ID") ?? "test@example.com";

// ID for the wallet object, must be in the form `issuerId.userId` where userId is alphanumeric.
var objectId = String.Format("{0}.{1}-{2}", issuerId, new Regex(@"[^\w.-]", RegexOptions.Compiled).Replace(userId, "_"), classId);
// [END setup]

///////////////////////////////////////////////////////////////////////////////
// Create authenticated HTTP client, using service account file.
///////////////////////////////////////////////////////////////////////////////

// [START auth]
var credentials = (ServiceAccountCredential) GoogleCredential
    .FromFile(serviceAccountJson)
    .CreateScoped(new[] { "https://www.googleapis.com/auth/wallet_object.issuer" })
    .UnderlyingCredential;

var httpClient = new HttpClient();
httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await credentials.GetAccessTokenForRequestAsync());
// [END auth]

///////////////////////////////////////////////////////////////////////////////
// Create a class via the API (this can also be done in the business console).
///////////////////////////////////////////////////////////////////////////////

// [START class]
var classUrl = "https://walletobjects.googleapis.com/walletobjects/v1/$object_typeClass/";
var classPayload = $class_payload;

var classResponse = await httpClient.PostAsJsonAsync(classUrl, classPayload);
var classContent = await classResponse.Content.ReadAsStringAsync();
Console.WriteLine("class POST response: " + classContent);
// [END class]

///////////////////////////////////////////////////////////////////////////////
// Create an object via the API.
///////////////////////////////////////////////////////////////////////////////

// [START object]
var objectUrl = "https://walletobjects.googleapis.com/walletobjects/v1/$object_typeObject/";
var objectPayload = $object_payload;

var objectResponse = await httpClient.GetAsync($"{objectUrl}{objectId}");
if ((int) objectResponse.StatusCode == 404) 
{
    objectResponse = await httpClient.PostAsJsonAsync(objectUrl, objectPayload);
}
var objectContent = await objectResponse.Content.ReadAsStringAsync();
Console.WriteLine("object GET or POST response: " + objectContent);
// [END object]

///////////////////////////////////////////////////////////////////////////////
// Create a JWT for the object, and encode it to create a "Save" URL.
///////////////////////////////////////////////////////////////////////////////

// [START jwt]
var claims = new JwtPayload();
claims.Add("iss", credentials.Id); // `client_email` in service account file.
claims.Add("aud", "google");
claims.Add("origins", new string[] { "www.example.com" });
claims.Add("typ", "savetowallet");
claims.Add("payload", new
{
    $object_typeObjects = new object[]
    {
        new
        {
            id = $object_id,
        },
    },
});

var key = new RsaSecurityKey(credentials.Key);
var signingCredentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256);
var jwt = new JwtSecurityToken(new JwtHeader(signingCredentials), claims);
var token = new JwtSecurityTokenHandler().WriteToken(jwt);
var saveUrl = $"https://pay.google.com/gp/v/save/{token}";
Console.WriteLine(saveUrl);
// [END jwt]
export const environment = {
  production: false,
  baseServUrl: 'http://localhost:5024/api/',
  baseImageUrl: 'http://localhost:4211/images/',
  // Google OAuth client id from Google Cloud Console (APIs & Services > Credentials).
  // Must match "Google:ClientId" in HC.Services/appsettings.json.
  // The client currently only allows https://homecuties.com and https://www.homecuties.com as
  // JavaScript origins - add http://localhost:4200 in the console to test the button locally.
  googleClientId: '286000626616-skdj1fljhcs5sjml2cs7shtme1lte3mu.apps.googleusercontent.com'
};

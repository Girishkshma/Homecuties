export const environment = {
  production: false,
  baseServUrl: 'http://localhost:5024/api/',
  // Where a product's photographs are served from while developing: the storefront's own dev server ('ng serve' on
  // 4211), which serves 'src/assets' as '{host}/images/...'. The API is pointed at that same folder by
  // 'ProductImages:Root' in appsettings.Development.json, so a photo uploaded in the admin area is on the storefront
  // page the moment it is written - the two folders have to be one, exactly as they do in production.
  baseImageUrl: 'http://localhost:4211/images/',
  // Google OAuth client id from Google Cloud Console (APIs & Services > Credentials).
  // Must match "Google:ClientId" in HC.Services/appsettings.json.
  // The client currently only allows https://homecuties.com and https://www.homecuties.com as
  // JavaScript origins - add http://localhost:4200 in the console to test the button locally.
  googleClientId: '286000626616-skdj1fljhcs5sjml2cs7shtme1lte3mu.apps.googleusercontent.com'
};

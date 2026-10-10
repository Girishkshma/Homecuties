export const environment = {
  production: true,
  baseServUrl: 'https://serv.homecuties.com/api/',
  // Where a product's photographs are served from: the API's own host, because that is the folder the API writes an
  // upload's sizes into ('{content root}/wwwroot/images/products' - see ProductImageService in HC.Business). It has
  // to be the folder 'ProductImages:Root' names if the shop ever sets that, or an upload would be written to one
  // folder and asked for from another.
  baseImageUrl: 'https://serv.homecuties.com/images/',
  // Google OAuth client id from Google Cloud Console (APIs & Services > Credentials).
  // Must match "Google:ClientId" in HC.Services/appsettings.json.
  googleClientId: '286000626616-skdj1fljhcs5sjml2cs7shtme1lte3mu.apps.googleusercontent.com'
};

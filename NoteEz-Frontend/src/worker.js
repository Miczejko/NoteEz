// Reverse proxy for /api/* so the browser only ever talks to noteez.online -
// the Azure backend is a different registrable domain, which makes its
// refreshToken cookie a third-party cookie from the browser's point of view.
// Many browsers block third-party cookies outright regardless of SameSite,
// so SameSite=None alone wasn't enough. Proxying server-side here makes the
// cookie first-party (Set-Cookie with no explicit Domain scopes to whatever
// host the browser actually requested, i.e. noteez.online) without needing
// a paid Azure App Service tier for a custom domain.
const API_ORIGIN = 'https://noteez-fdd2crbzhubpgaan.germanywestcentral-01.azurewebsites.net'

export default {
  async fetch(request, env) {
    const url = new URL(request.url)

    if (url.pathname.startsWith('/api/')) {
      const upstreamUrl = API_ORIGIN + url.pathname + url.search
      const upstreamResponse = await fetch(new Request(upstreamUrl, request))
      // Rebuild from the whole Response (not just its headers) so multiple
      // Set-Cookie entries pass through to the browser intact.
      return new Response(upstreamResponse.body, upstreamResponse)
    }

    return env.ASSETS.fetch(request)
  },
}

const SpotifyConfig = {
    pluginUniqueId: '8a586678-5b5f-4a40-afaa-5db100a21b34',
    pluginApiBaseUrl: 'SpotifyAuth' // SpotifyAuthController
};

const apiQueryOpts = {};

// document.querySelector('#SpotifyConfigPage')
//     .addEventListener('pageshow', function () {
//         Dashboard.showLoadingMsg();
//         ApiClient.getPluginConfiguration(SpotifyConfig.pluginUniqueId).then(function (config) {
//             document.querySelector('#Options').value = config.Options;
//             document.querySelector('#AnInteger').value = config.AnInteger;
//             document.querySelector('#TrueFalseSetting').checked = config.TrueFalseSetting;
//             document.querySelector('#AString').value = config.AString;
//             Dashboard.hideLoadingMsg();
//         });
//     });

// document.querySelector('#SpotifyConfigForm')
//     .addEventListener('submit', function (e) {
//         Dashboard.showLoadingMsg();
//         ApiClient.getPluginConfiguration(SpotifyConfig.pluginUniqueId).then(function (config) {
//             config.Options = document.querySelector('#Options').value;
//             config.AnInteger = document.querySelector('#AnInteger').value;
//             config.TrueFalseSetting = document.querySelector('#TrueFalseSetting').checked;
//             config.AString = document.querySelector('#AString').value;
//             ApiClient.updatePluginConfiguration(SpotifyConfig.pluginUniqueId, config).then(function (result) {
//                 Dashboard.processPluginConfigurationUpdateResult(result);
//             });
//         });

//         e.preventDefault();
//         return false;
//     });

function onLoginClick() {
    Dashboard.showLoadingMsg();
    ApiClient.getJSON(ApiClient.getUrl(SpotifyConfig.pluginApiBaseUrl + '/StartAuth'), apiQueryOpts).then(function (result) {
        Dashboard.hideLoadingMsg();
        window.open(result.VerificationUriComplete, '_blank');

        Dashboard.alert({
            title: 'Spotify Login',
            message: 'You will be redirected to Spotify to authorize Jellyfin to access your account. After logging in, click "Got It" or reload the page.',
            callback: updateSettings
        });
    });
}

function onLogoutClick() {
    Dashboard.showLoadingMsg();
    ApiClient.getJSON(ApiClient.getUrl(SpotifyConfig.pluginApiBaseUrl + '/RemoveAuth'), apiQueryOpts).then(function (result) {
        Dashboard.hideLoadingMsg();
        updateSettings();
    });
}

function updateSettings() {
    Dashboard.showLoadingMsg();
    ApiClient.getPluginConfiguration(SpotifyConfig.pluginUniqueId).then(function (config) {
        Dashboard.hideLoadingMsg();

        // if (config.EnableVerboseLogging) {
        //     document.querySelector('#dbgSection').classList.remove('hide');
        // }

        document.querySelector('#SpotifyDeviceId').value = config.DeviceId || 'Unknown';
        document.querySelector('#SpotifyUsername').value = config.SpotifyCredentials == null ? 'Not Logged In' : (config.SpotifyCredentials.username || 'Unknown');

        document.querySelector('#SpotifyUsernameLinkContainer').classList.add('hide');
        let username = config.SpotifyCredentials?.username || null;
        if (username != null) {
            document.querySelector('#SpotifyUsernameLinkContainer').classList.remove('hide');
            document.querySelector('#SpotifyUsernameLink').href = `https://open.spotify.com/user/${encodeURIComponent(username)}`;
        }

        document.querySelector('#SpotifyLogIn').classList.add('hide');
        document.querySelector('#SpotifyReauth').classList.add('hide');
        document.querySelector('#SpotifyDeauth').classList.add('hide');

        if (config.SpotifyCredentials == null) {
            document.querySelector('#SpotifyLogIn').classList.remove('hide');
        }
        else {
            document.querySelector('#SpotifyReauth').classList.remove('hide');
            document.querySelector('#SpotifyDeauth').classList.remove('hide');
        }
    });
}

export default function (view) {
    view.dispatchEvent(new CustomEvent('create'));

    view.addEventListener('viewshow', function () {
        apiQueryOpts.UserId = Dashboard.getCurrentUserId();
        apiQueryOpts.api_key = ApiClient.accessToken();

        updateSettings();
    });

    document.querySelector('#SpotifyLogIn').addEventListener('click', onLoginClick);
    document.querySelector('#SpotifyReauth').addEventListener('click', onLoginClick);
    document.querySelector('#SpotifyDeauth').addEventListener('click', onLogoutClick);
}
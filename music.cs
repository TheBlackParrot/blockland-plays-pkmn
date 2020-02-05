AudioMusicLooping3d.is3d = 0;
AudioMusicLooping3d.referenceDistance = 999999;
AudioMusicLooping3d.maxDistance = 999999;
$Server::Pokemon::MusicVolume = 1;

function initPokemonMusic() {
	$Server::Pokemon::MusicInit = 1;
	%pattern = "Add-Ons/Gamemode_BL_Plays_Pokemon/music/*.ogg";

	for(%file = findFirstFile(%pattern); isFile(%file); %file = findNextFile(%pattern)) {
		%base = fileBase(%file);

		%name = "musicData_PKMN_" @ %base;
		eval("datablock AudioProfile(" @ %name @ ") {fileName = \"" @ %file @ "\"; description = \"AudioMusicLooping3d\"; preload = 1; uiName = \"" @ %base @ "\";};");

		// 1f_fc_08_4a
		// 0123456789a
		for(%i = 0; %i < strLen(%base); %i += 6) {
			%part = getSubStr(%base, %i, 5);
			%bank = getSubStr(%part, 0, 2);
			%track = getSubStr(%part, 3, 2);

			$Server::Pokemon::Music[%bank,%track] = %name;
			echo("Music added for" SPC %bank @ "," @ %track);
		}
	}

	%pattern = "Add-Ons/Gamemode_BL_Plays_Pokemon/sounds/*.wav";
	for(%file = findFirstFile(%pattern); isFile(%file); %file = findNextFile(%pattern)) {
		%base = fileBase(%file);

		%name = "soundData_PKMN_" @ %base;
		eval("datablock AudioProfile(" @ %name @ ") {fileName = \"" @ %file @ "\"; description = \"AudioClosest3d\"; preload = 1; uiName = \"" @ %base @ "\";};");

		// 1f_fc_08_4a
		// 0123456789a
		for(%i = 0; %i < strLen(%base); %i += 6) {
			%part = getSubStr(%base, %i, 5);
			%bank = getSubStr(%part, 0, 2);
			%track = getSubStr(%part, 3, 2);

			$Server::Pokemon::Sound[%bank,%track] = %name;
			echo("Sound added for" SPC %bank @ "," @ %track);
		}
	}

	if(isObject($Server::Pokemon::Music)) {
		$Server::Pokemon::Music.delete();
	}
	$Server::Pokemon::Music = new AudioEmitter() {
		is3D = 0;
		profile = "";
		referenceDistance = 999999;
		maxDistance = 999999;
		volume = $Server::Pokemon::MusicVolume;
		position = $loadOffset;
	};
}
if($Server::Pokemon::MusicInit $= "") {
	initPokemonMusic();
}
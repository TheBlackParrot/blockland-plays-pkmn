/*
! GUI isn't disappearing on other servers
*/

// https://bulbapedia.bulbagarden.net/wiki/Character_encoding_in_Generation_I
const chars = " ABCDEFGHIJKLMNOPQRSTUVWXYZ():;[]abcdefghijklmnopqrstuvwxyzedlstv                                'PM-rm?!.zzz>>VM$x./,F0123456789";
const encodingOffset = 0x7F; // (7F - 7F = 0, chars[0] = " ", 7F is a space character in-game)

// this is NOT FOR STRINGS IN POKEMON! this is what screen data is being translated to
const screenChars = "0123456789abcdefghijklmnopqrstuvwxyz!@#$%^&*()-_=+{}[]|:;<,>.?/~";

const romFilename = "./pkmn-violet.gb";

var WasmBoy = require('wasmboy').WasmBoy;

const WasmBoyOptions = {
	headless: true,
	useGbcWhenOptional: false,
	isAudioEnabled: false,
	frameSkip: 1,
	audioBatchProcessing: true,
	timersBatchProcessing: false,
	audioAccumulateSamples: true,
	graphicsBatchProcessing: false,
	graphicsDisableScanlineRendering: false,
	tileRendering: true,
	tileCaching: true,
	gameboyFPSCap: 15,
	updateGraphicsCallback: false,
	updateAudioCallback: false,
	saveStateCallback: false
};

const GAMEBOY_CAMERA_WIDTH = 160;
const GAMEBOY_CAMERA_HEIGHT = 144;

const { createCanvas, loadImage } = require('canvas');
const canvas = createCanvas(160, 144);
const ctx = canvas.getContext('2d');

const fs = require("fs");

const net = require("net");

const rom = new Uint8Array((fs.readFileSync(romFilename)));

var WasmBoyJoypadState = {
	UP: false,
	RIGHT: false,
	DOWN: false,
	LEFT: false,
	A: false,
	B: false,
	SELECT: false,
	START: false
}

var delay = 3500;

async function runStep() {
	//randomizeInput();
	WasmBoy.setJoypadState(WasmBoyJoypadState);
	await WasmBoy.pause();
	await sendFrame();
	//fs.writeFileSync("/tmp/out.json", JSON.stringify(frame));
	//canvas.createPNGStream().pipe(fs.createWriteStream("/tmp/image.png"));
	await WasmBoy.play();	
}

var gbMemoryStart;
var gbMemorySize;
var gbMemoryEnd;
var pkmnNames = [undefined];
var moveNames = [undefined];
var conditionNames = [undefined];
var typeNames;
var itemNames = [undefined];
var tmhmTeach = [];

function parsePKMNString(inArr, terminateList = [0x50]) {
	let out = "";
	for(let idx in inArr) {
		let v = inArr[idx] - encodingOffset;

		if(typeof chars[v] !== "undefined") {
			out += chars[v];
		}

		if(terminateList.indexOf(inArr[idx]) !== -1) {
			return out;
		}
	}
	return out;
}

function parseRomContent(romContent, outArr, startAddr, endAddr, maxLen = 255, terminateList = [0x50]) {
	let ongoing = "";

	for(let idx = startAddr; idx < endAddr; idx++) {
		let val = romContent[idx];
		let offsetVal = val - encodingOffset;

		if(terminateList.indexOf(val) !== -1 || ongoing.length >= maxLen) {
			if(ongoing !== "") {
				outArr.push(ongoing.slice(0));
				ongoing = "";
			}
			if(terminateList.indexOf(val) !== -1) {
				continue;
			}
		}

		if(val < 0) {
			continue;
		}

		ongoing += chars[offsetVal];
	}	
}

WasmBoy.config(WasmBoyOptions, canvas).then(() => {
	console.log('WasmBoy is configured!');
	WasmBoy.loadROM(rom).then(function() {
		console.log("ROM loaded!");

		WasmBoy.play().then(function() {
			console.log("Playing!");

			setTimeout(async function() {
				gbMemoryStart = await WasmBoy._getWasmConstant('DEBUG_GAMEBOY_MEMORY_LOCATION');
				gbMemorySize = await WasmBoy._getWasmConstant('DEBUG_GAMEBOY_MEMORY_SIZE');
				gbMemoryEnd = gbMemoryStart + gbMemorySize;

				try {
					//let state = JSON.parse(fs.readFileSync("./latest.sav"));

					let files = fs.readdirSync("./saves");
					files.sort(function(a, b) { 
						a = parseInt(a.replace(".sav", ""));
						b = parseInt(b.replace(".sav", ""));
						return b - a;
					});

					let latest = files[0];
					console.log(`loaded ${latest}`);

					let state = JSON.parse(fs.readFileSync(`./saves/${latest}`));

					let nwIS = Uint8Array.from(state.wasmboyMemory.wasmBoyInternalState);
					state.wasmboyMemory.wasmBoyInternalState = nwIS;
					let nwPM = Uint8Array.from(state.wasmboyMemory.wasmBoyPaletteMemory);
					state.wasmboyMemory.wasmBoyPaletteMemory = nwPM;
					let ngBM = Uint8Array.from(state.wasmboyMemory.gameBoyMemory);
					state.wasmboyMemory.gameBoyMemory = ngBM;
					let ncR = Uint8Array.from(state.wasmboyMemory.cartridgeRam);
					state.wasmboyMemory.cartridgeRam = ncR;

					await WasmBoy.loadState(state);
				} catch(e) {
					console.error(e);
				}

				setInterval(gameLoop, 50);
			}, 1000);

			fs.readFile(romFilename, function(err, romContent) {
				parseRomContent(romContent, pkmnNames, 0x1C21E, 0x1C98B, 10);
				console.log(`Parsed ${pkmnNames.filter(function(x) { return x !== "MISSINGNO."}).length} Pokemon names`);
				fs.writeFileSync("/tmp/names.json", JSON.stringify(pkmnNames));
				//fs.writeFileSync("/tmp/names-filtered.json", JSON.stringify(pkmnNames.filter(function(x) { return x !== "MISSINGNO."})));

				parseRomContent(romContent, moveNames, 0xB0000, 0xB060E);
				console.log(`Parsed ${moveNames.length} move names`);
				//fs.writeFileSync("/tmp/move-names.json", JSON.stringify(moveNames));

				parseRomContent(romContent, conditionNames, 0x5DDAC, 0x5DDCA, 3, [0x50, 0x4E, 0x7F]);
				console.log(`Parsed ${conditionNames.length} conditions`); // QUIT is grouped up here so im just going to leave it in case

				//parseRomContent(romContent, typeNames, 0x27DE4, 0x27E4A);
				//typeNames.splice(typeNames.indexOf("BIRD"), 1);
				//console.log(`Parsed ${typeNames.length} types`);
				//console.log(typeNames.map(function(t) { return `$typeColor["${t}"] = "";`}).join('\n'));
				//console.log(typeNames);
				// fun fact! there's no structure to this. so i have to manually define this.
				typeNames = {
					0: "NORMAL",
					1: "FIGHTING",
					2: "FLYING",
					3: "POISON",
					4: "GROUND",
					5: "ROCK",
					7: "BUG",
					8: "GHOST",
					20: "FIRE",
					21: "WATER",
					22: "GRASS",
					23: "ELECTRIC",
					24: "PSYCHIC",
					25: "ICE",
					26: "DRAGON"
				};

				parseRomContent(romContent, itemNames, 0x472B, 0x4A92, 12);
				console.log(`Parsed ${itemNames.length} items`);
				//console.log(itemNames.join("\n"));

				for(let idx = 0x13773; idx <= 0x137A9; idx++) {
					tmhmTeach.push(romContent[idx]);
				}
				console.log(tmhmTeach.length);
			});
		})
	}).catch(function() {
		console.error('Error loading the ROM');
	})
}).catch(() => {
	console.error('Error Configuring WasmBoy...');
});

function randomizeInput() {
	for(let input in WasmBoyJoypadState) {
		WasmBoyJoypadState[input] = Math.floor(Math.random() * 2);
	}
	console.log(WasmBoyJoypadState);
}

var oldMusicBank = "z";
var oldMusicTrack = "z";
var curMusicBank = "z";
var curMusicTrack = "z";
var oldSoundData = "z";
var oldSoundTriggerData = 0;
var triggeredSoundLastCheck = 0; // hhhhhhh
async function gameLoop() {
	// addresses starting at C0BA-C0BD are 1 when no sound is playing, otherwise they have some value
	// C0B6 - C0B9 appear to be music related, while C0BA - C0BD appear to be sfx related. perfect!

	// address CCF6 maps to the low health alarm?

	await WasmBoy._runWasmExport('updateDebugGBMemory');

	WasmBoy._getWasmMemorySection(gbMemoryStart + 0xc0ef, gbMemoryStart + 0xc0f0).then(function(v) {
		curMusicBank = v[0].toString(16).padStart(2, '0');
		WasmBoy._getWasmMemorySection(gbMemoryStart + 0xc026, gbMemoryStart + 0xc027).then(function(v) {
			curMusicTrack = v[0].toString(16).padStart(2, '0');

			if(curMusicBank !== oldMusicBank || curMusicTrack !== oldMusicTrack) {
				console.log(curMusicBank, curMusicTrack);
				for(let idx in TCPClients) {
					let cl = TCPClients[idx];
					cl.send(`m\t${curMusicBank}\t${curMusicTrack}`);
				}
			}

			oldMusicBank = curMusicBank;
			oldMusicTrack = curMusicTrack;

			WasmBoy._getWasmMemorySection(gbMemoryStart + 0xc0ba, gbMemoryStart + 0xc0be).then(function(l) {
				WasmBoy._getWasmMemorySection(gbMemoryStart + 0xc0ec, gbMemoryStart + 0xc0ee).then(function(v) {
					// 65_176		select
					// 65_173		start menu
					// 66_31		enter/leave
					// 66_28		bump
					// 65_197		run?

					let curSoundData = `${v[0].toString(16).padStart(2, '0')}\t${v[1].toString(16).padStart(2, '0')}`;
					let curSoundTriggerData = 0;
					l.map(function(_) { curSoundTriggerData += _; });

					if(curSoundData !== oldSoundData || (curSoundTriggerData > oldSoundTriggerData && !triggeredSoundLastCheck)) {
						console.log(`triggered ${curSoundData}`);
						for(let idx in TCPClients) {
							let cl = TCPClients[idx];
							cl.send(`s\t${curSoundData}`);
						}
						oldSoundData = curSoundData;
						triggeredSoundLastCheck = 1;
					} else {
						triggeredSoundLastCheck = 0;
					}

					oldSoundTriggerData = curSoundTriggerData;
				})
			});
		});
	});
	//fs.writeFileSync("/tmp/mem.txt", JSON.stringify(memtest));
}

function getPokemon(callback) {
	let out = [];
	let minRange = 0xD16B;
	let maxRange = 0xD272;
	let vals = 0xD197 - minRange;
	let mainIdx = 0;
	WasmBoy._getWasmMemorySection(gbMemoryStart + 0xd163, gbMemoryStart + minRange).then(function(v) {
		let count = v[0] * vals;
		WasmBoy._getWasmMemorySection(gbMemoryStart + minRange, gbMemoryStart + maxRange + 1).then(function(pkmn) {
			WasmBoy._getWasmMemorySection(gbMemoryStart + 0xD2B5, gbMemoryStart + 0xD2F6 + 1).then(function(nicks) {
				for(let idx = 0; idx < count; idx += vals) {
					console.log(pkmn[idx + 6]);
					out.push({
						name: parsePKMNString(nicks.slice(mainIdx*11), [0x50]),
						species: pkmnNames[pkmn[idx]],
						hp: {
							current: pkmn[idx + 1] + pkmn[idx + 2],
							max: pkmn[idx + 34] + pkmn[idx + 35]
						},
						status: conditionNames[pkmn[idx + 4]],
						ptypes: [typeNames[pkmn[idx + 5]], typeNames[pkmn[idx + 6]]],
						moves: [
							{
								move: moveNames[pkmn[idx + 8]],
								pp: pkmn[idx + 29]
							},
							{
								move: moveNames[pkmn[idx + 9]],
								pp: pkmn[idx + 30]
							},
							{
								move: moveNames[pkmn[idx + 10]],
								pp: pkmn[idx + 31]
							},
							{
								move: moveNames[pkmn[idx + 11]],
								pp: pkmn[idx + 32]
							}
						],
						exp: pkmn[idx + 14] + pkmn[idx + 15] + pkmn[idx + 16],
						level: pkmn[idx + 33],
						stats: {
							attack: pkmn[idx + 36] + pkmn[idx + 37],
							defense: pkmn[idx + 38] + pkmn[idx + 39],
							speed: pkmn[idx + 40] + pkmn[idx + 41],
							special: pkmn[idx + 42] + pkmn[idx + 43]
						}
					});
					mainIdx++;
				}

				if(typeof callback === "function") {
					callback(out);
				}
			})
		});
	});
}

function getItems(callback) {
	let out = [];
	let minRange = 0xD31D;
	let maxRange = 0xD347;

	// values of >200 on items are TM's Item#-200 = TM#
	// 195 == hm01
	// 200 == hm05

	WasmBoy._getWasmMemorySection(gbMemoryStart + minRange, gbMemoryStart + maxRange).then(function(v) {
		console.log(v.join("\n"));
		let count = v[0];
		for(let idx = 0; idx < count; idx++) {
			let itemValue = v[1 + (idx * 2)];

			let toPush = {
				name: "",
				amount: 0
			};

			if(itemValue > 200) {
				toPush.name = `TM${(itemValue-200).toString().padStart(2, '0')}`;
				toPush.move = moveNames[tmhmTeach[itemValue-201]];
			} else if(itemValue >= 195 && itemValue <= 200) {
				toPush.name = `HM${(itemValue-195).toString().padStart(2, '0')}`;
				toPush.move = moveNames[tmhmTeach[itemValue-195+49]];
			} else {
				toPush.name = itemNames[itemValue];
			}
			toPush.amount = v[2 + (idx * 2)];

			out.push(toPush);
		}

		if(typeof callback === "function") {
			callback(out);
		}
	});
}

function getWildPokemon(callback) {
	let out = {
		"common": [],
		"uncommon": [],
		"rare": []
	};
	let minRange = 0xD888;
	let maxRange = 0xD89C;

	WasmBoy._getWasmMemorySection(gbMemoryStart + minRange, gbMemoryStart + maxRange).then(function(v) {
		for(let idx = 0; idx < 4; idx++) {
			out.common.push({
				name: pkmnNames[v[1 + (idx * 2)]],
				level: v[idx * 2],
			});
		}

		for(let idx = 0; idx < 4; idx++) {
			out.uncommon.push({
				name: pkmnNames[v[9 + (idx * 2)]],
				level: v[8 + (idx * 2)]
			});
		}

		for(let idx = 0; idx < 2; idx++) {
			out.rare.push({
				name: pkmnNames[v[17 + (idx * 2)]],
				level: v[16 + (idx * 2)]
			});
		}

		if(typeof callback === "function") {
			callback(out);
		}
	});	
}

function getPlayer(callback) {
	// D356 -- badges
	// D34A - D351 -- rival name
	// D347 - D349 -- holla holla get dollar (this is the STRAIGHT UP VALUE. NO MATH. 47 48 49)
	// DA40 - DA43 -- time (40+41 : 42+43 base16)
	// D158 - D162 -- player name
	// D2F7 - D309 -- dex own
	// D30A - D31C -- dex seen

	let minRange = 0xD158;
	let maxRange = 0xDA44;

	let out = {
		names: {
			player: "",
			rival: ""
		},
		badges: "",
		money: 0,
		time: 0,
		dex: {
			seenCount: 0,
			ownCount: 0
		}
	};

	WasmBoy._getWasmMemorySection(gbMemoryStart + minRange, gbMemoryStart + maxRange).then(function(v) {
		// lazy
		out.names.player = parsePKMNString(v.slice(0xD158 - minRange, 0xD162 - minRange + 1));
		out.names.rival = parsePKMNString(v.slice(0xD34A - minRange, 0xD351 - minRange + 1));
		
		out.badges = parseInt(v[0xD356 - minRange]).toString(2).padStart(8, '0');
		out.money = parseInt(v.slice(0xD347 - minRange, 0xD349 - minRange + 1).map(function(x) { return x.toString(16); }).join("")).toLocaleString();
		out.time = `${v[0xDA40 - minRange] + v[0xDA41 - minRange]}h ${v[0xDA42 - minRange] + v[0xDA43 - minRange]}m`;

		for(let idx = 0xD2F7 - minRange; idx <= 0xD309 - minRange; idx++) {
			out.dex.ownCount += v[idx].toString(2).replace(/0/g, "").length;
		}
		for(let idx = 0xD30A - minRange; idx <= 0xD31C - minRange; idx++) {
			out.dex.seenCount += v[idx].toString(2).replace(/0/g, "").length;
		}

		if(typeof callback === "function") {
			callback(out);
		}
	});
}

const sendFrame = async () => {
	// Get our output frame
	const frameInProgressVideoOutputLocation = await WasmBoy._getWasmConstant('FRAME_LOCATION');
	const frameInProgressMemory = await WasmBoy._getWasmMemorySection(
		frameInProgressVideoOutputLocation,
		frameInProgressVideoOutputLocation + GAMEBOY_CAMERA_HEIGHT * GAMEBOY_CAMERA_WIDTH * 3 + 1
	);

	let out = [];
	for(let y = 0; y < GAMEBOY_CAMERA_HEIGHT; y++) {
		for(let x = 0; x < GAMEBOY_CAMERA_WIDTH; x += 3) {
			let start = (y * GAMEBOY_CAMERA_WIDTH + x) * 3;
			let result = 0;

			let maxIters = 3;
			if(x >= 159) {
				maxIters = 2;
			}
			for(let idx = 0; idx < maxIters; idx++) {
				switch(frameInProgressMemory[start + (idx*3)]) {
					case 242:
					case 255:
						result += (3 << idx*2);
						break;

					case 160:
						result += (2 << idx*2);
						break;

					case 88:
						result += (1 << idx*2);
						break;

					case 8:
					case 0:
						result += (0 << idx*2);
						break;
				}
			}

			out.push(screenChars[result]);

			//ctx.fillStyle = `rgba(${[frameInProgressMemory[start], frameInProgressMemory[start+1], frameInProgressMemory[start+2], 1].join(", ")})`;
			//ctx.fillRect(x, y, 1, 1);
		}
	}

	for(let idx in TCPClients) {
		let cl = TCPClients[idx];
		cl.send(`r\t${out.join("")}`);
	}
};

var isMoving = false;
function resetJoypadState() {
	WasmBoyJoypadState = {
		UP: false,
		RIGHT: false,
		DOWN: false,
		LEFT: false,
		A: false,
		B: false,
		SELECT: false,
		START: false
	}
	WasmBoy.setJoypadState(WasmBoyJoypadState);
	isMoving = false;
}

var lastSaveTimestamp = 0;
function saveGame(bypass) {
	let now = Date.now();
	if(now - lastSaveTimestamp > 300000 || bypass) {
		WasmBoy.saveState().then(function(state) {
			let nwIS = Array.from(state.wasmboyMemory.wasmBoyInternalState);
			state.wasmboyMemory.wasmBoyInternalState = nwIS;
			let nwPM = Array.from(state.wasmboyMemory.wasmBoyPaletteMemory);
			state.wasmboyMemory.wasmBoyPaletteMemory = nwPM;
			let ngBM = Array.from(state.wasmboyMemory.gameBoyMemory);
			state.wasmboyMemory.gameBoyMemory = ngBM;
			let ncR = Array.from(state.wasmboyMemory.cartridgeRam);
			state.wasmboyMemory.cartridgeRam = ncR;

			//fs.writeFileSync("./latest.sav", JSON.stringify(state));
			fs.writeFileSync(`./saves/${now}.sav`, JSON.stringify(state));

			for(let idx in TCPClients) {
				let cl = TCPClients[idx];
				cl.send(`a\t${now}.sav`);
			}

			WasmBoy.play();
		});
		lastSaveTimestamp = now;
	}	
}

var votes = {};
var framesToGo = 10;
var inputMode = "A";

var funcs = {
	"done": function(args) {
		framesToGo--;
		if(framesToGo <= 0 && inputMode === "D") {
			framesToGo = 10;

			WasmBoyJoypadState = {
				UP: false,
				RIGHT: false,
				DOWN: false,
				LEFT: false,
				A: false,
				B: false,
				SELECT: false,
				START: false
			}

			let count = {
				UP: 0,
				RIGHT: 0,
				DOWN: 0,
				LEFT: 0,
				A: 0,
				B: 0,
				SELECT: 0,
				START: 0,
				"NO INPUT": 0
			};

			for(let whom in votes) {
				count[votes[whom]]++;
			}

			let highestInputNames = [];
			let highestInputName;
			let highestInputCount = 0;
			for(let input in count) {
				if(count[input] > highestInputCount) {
					highestInputCount = count[input];
					highestInputNames = [input];
				} else if(count[input] == highestInputCount) {
					highestInputNames.push(input);
				}
			}

			if(highestInputCount) {
				if(highestInputNames.length > 1) {
					highestInputName = highestInputNames[Math.floor(Math.random() * highestInputNames.length)];
				} else {
					highestInputName = highestInputNames[0];
				}
				if(highestInputName !== "NO INPUT") {
					WasmBoyJoypadState[highestInputName] = true;
				}

				for(let idx in TCPClients) {
					let cl = TCPClients[idx];
					cl.send(`i\t${highestInputName}\t${highestInputCount}`);
				}

				votes = {};
				saveGame();
			}

			WasmBoy.setJoypadState(WasmBoyJoypadState);
			setTimeout(resetJoypadState, 150);
		}

		if(inputMode === "D") {
			for(let idx in TCPClients) {
				let cl = TCPClients[idx];
				cl.send(`f\t${framesToGo}`);
			}
		}

		runStep();
	},

	"vote": function(args) {
		let votedOn;
		let testingOn = args[1].split(" ").slice(0, 1)[0].toLowerCase();
		switch(testingOn) {
			case "u":
			case "up":
			case "f":
			case "fo":
			case "for":
			case "forw":
			case "forwa":
			case "forwar":
			case "forward":
			case "forwards":
				votedOn = "UP";
				break;

			case "r":
			case "ri":
			case "rig":
			case "righ":
			case "right":
				votedOn = "RIGHT";
				break;

			case "l":
			case "le":
			case "lef":
			case "left":
				votedOn = "LEFT";
				break;

			case "d":
			case "do":
			case "dow":
			case "down":
			case "ba":
			case "bac":
			case "back":
			case "backw":
			case "backwa":
			case "backwar":
			case "backward":
			case "backwards":
				votedOn = "DOWN";
				break;

			case "a":
			case "y":
				votedOn = "A";
				break;

			case "b":
			case "x":
			case "n":
				votedOn = "B";
				break;

			case "se":
			case "sel":
			case "sele":
			case "selec":
			case "select":
			case "-":
				votedOn = "SELECT";
				break;

			case "st":
			case "sta":
			case "star":
			case "start":
			case "+":
			case "e":
			case "en":
			case "ent":
			case "ente":
			case "enter":
			case "m":
			case "men":
			case "menu":
				votedOn = "START";
				break;

			case "no":
			case "not":
			case "noth":
			case "nothi":
			case "nothin":
			case "nothing":
			case "st":
			case "sta":
			case "stay":
			case "sto":
			case "stop":
			case "h":
			case "ho":
			case "hol":
			case "hold":
			case "wa":
			case "wai":
			case "wait":
				votedOn = "NO INPUT";
				break;
		}

		if(typeof votedOn !== "undefined") {
			if(inputMode === "D") {
				votes[args[0]] = votedOn;

				for(let idx in TCPClients) {
					let cl = TCPClients[idx];
					cl.send(`v\t${args[0]}\t${votedOn}`);
				}
			}

			if(inputMode === "A" && !isMoving) {
				if(votedOn !== "NO INPUT") {
					WasmBoyJoypadState[votedOn] = true;
					WasmBoy.setJoypadState(WasmBoyJoypadState);
					let checks = args[1].split(" ");
					if(checks.length > 1) {
						args[1] = checks.slice(1, 7).join(" ");
						setTimeout(function() {
							funcs["vote"](args)
						}, 225);
					}
					setTimeout(resetJoypadState, 200);
				} else {
					resetJoypadState();
				}

				for(let idx in TCPClients) {
					let cl = TCPClients[idx];
					cl.send(`i\t${votedOn}\t-1`);
				}

				saveGame();
			}
		}
	},

	"mode": function(args) {
		inputMode = args[0];

		if(inputMode === "A") {
			for(let idx in TCPClients) {
				let cl = TCPClients[idx];
				cl.send(`f\t-`);
			}
		}
	},

	"party": function(args) {
		getPokemon(function(out) {
			for(let idx in TCPClients) {
				let cl = TCPClients[idx];
				cl.send(`p\t${args[0]}\t${JSON.stringify(out)}`);
			}			
		});
	},

	"items": function(args) {
		getItems(function(out) {
			for(let idx in TCPClients) {
				let cl = TCPClients[idx];
				cl.send(`t\t${args[0]}\t${JSON.stringify(out)}`);
			}			
		});
	},

	"wild": function(args) {
		getWildPokemon(function(out) {
			for(let idx in TCPClients) {
				let cl = TCPClients[idx];
				cl.send(`w\t${args[0]}\t${JSON.stringify(out)}`);
			}			
		});
	},

	"player": function(args) {
		getPlayer(function(out) {
			for(let idx in TCPClients) {
				let cl = TCPClients[idx];
				cl.send(`l\t${args[0]}\t${JSON.stringify(out)}`);
			}			
		});
	},

	"save": function(args) {
		saveGame(true);
	}
}

var TCPClients = [];
net.createServer(function(socket) {
	TCPClients.push(socket);
	socket.write("OK\r\n");

	socket.send = function(data) {
		socket.write(data + "\r\n");
	}

	socket.on("data", function(data) {
		let lines = data.toString().split("\n").map(x => x.trim());
		for(let idx in lines) {
			let line = lines[idx].toString().trim();

			if(!line) {
				return;
			}

			var parts = line.split("\t").map(x => x.trim());

			if(parts[0] in funcs) {
				funcs[parts[0]](parts.slice(1));
			}
		}
	});

	socket.on("error", function(err) {
		TCPClients.splice(TCPClients.indexOf(socket), 1);
	});

	socket.on("end", function(err) {
		TCPClients.splice(TCPClients.indexOf(socket), 1);
	});
}).listen(32145, "127.0.0.1");
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Brix.Engine;
using Brix.Engine.Nature;
using Brix.External.Factories;
using Brix.Game;
using Brix.Game.AI;
using Brix.Game.AI.Damage;
using Brix.Game.Components;
using Brix.Input;
using Brix.Input.ContextualOrders;
using Brix.Legacy;
using Brix.Lifecycle.Pooling;
using Brix.Lua;
using Brix.Network;
using Brix.Pathfinding.Test;
using Brix.Utils;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.PostProcessing;
using UnityEngine.SceneManagement;

namespace BetterThanCastle
{
public partial class ResourcePackManager : MonoBehaviour
{

	private static bool ApplyExtraHpKey(string k, string key, string val, ResourcePackManager.HpConfig cfg, int lineIdx, List<string> problems)
	{
		bool flag = k.StartsWith("bombcolor");
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4 = flag3;
		bool flag5 = flag4;
		bool flag6 = flag5;
		bool flag7 = flag6;
		bool flag8 = flag7;
		bool result;
		if (flag8)
		{
			int num;
			bool flag9 = int.TryParse(k.Substring(9), out num) && num >= 0 && num < 8;
			bool flag10 = flag9;
			bool flag11 = flag10;
			bool flag12 = flag11;
			bool flag13 = flag12;
			bool flag14 = flag13;
			bool flag15 = flag14;
			bool flag16 = flag15;
			if (flag16)
			{
				Color color;
				bool flag17 = ResourcePackManager.TryParseHpColor(val, out color);
				bool flag18 = flag17;
				bool flag19 = flag18;
				bool flag20 = flag19;
				bool flag21 = flag20;
				bool flag22 = flag21;
				bool flag23 = flag22;
				bool flag24 = flag23;
				if (flag24)
				{
					color.a = 1f;
					cfg.bombColors[num] = color;
				}
				else
				{
					problems.Add(string.Concat(new object[]
					{
						"line ",
						lineIdx + 1,
						": bad color '",
						val,
						"'"
					}));
				}
			}
			result = true;
		}
		else
		{
			bool flag25 = k.StartsWith("bombdamagestep");
			bool flag26 = flag25;
			bool flag27 = flag26;
			bool flag28 = flag27;
			bool flag29 = flag28;
			bool flag30 = flag29;
			bool flag31 = flag30;
			bool flag32 = flag31;
			if (flag32)
			{
				int num2;
				bool flag33 = int.TryParse(k.Substring(14), out num2) && num2 >= 1 && num2 <= 7;
				bool flag34 = flag33;
				bool flag35 = flag34;
				bool flag36 = flag35;
				bool flag37 = flag36;
				bool flag38 = flag37;
				bool flag39 = flag38;
				bool flag40 = flag39;
				if (flag40)
				{
					cfg.bombDmg[num2 - 1] = ResourcePackManager.ParseHpFloat(val, cfg.bombDmg[num2 - 1], 1f, 4000f);
				}
				result = true;
			}
			else
			{
				bool flag41 = k.StartsWith("bombthreshold");
				bool flag42 = flag41;
				bool flag43 = flag42;
				bool flag44 = flag43;
				bool flag45 = flag44;
				bool flag46 = flag45;
				bool flag47 = flag46;
				bool flag48 = flag47;
				if (flag48)
				{
					int num3;
					bool flag49 = int.TryParse(k.Substring(13), out num3) && num3 >= 1 && num3 <= 7;
					bool flag50 = flag49;
					bool flag51 = flag50;
					bool flag52 = flag51;
					bool flag53 = flag52;
					bool flag54 = flag53;
					bool flag55 = flag54;
					bool flag56 = flag55;
					if (flag56)
					{
						cfg.bombThr[num3 - 1] = ResourcePackManager.ParseHpFloat(val, cfg.bombThr[num3 - 1], 0.1f, 100f);
					}
					result = true;
				}
				else
				{
					bool flag57 = ResourcePackManager.ApplyCombatKey(k, val, cfg, lineIdx, problems);
					bool flag58 = flag57;
					bool flag59 = flag58;
					bool flag60 = flag59;
					bool flag61 = flag60;
					bool flag62 = flag61;
					bool flag63 = flag62;
					bool flag64 = flag63;
					if (flag64)
					{
						result = true;
					}
					else
					{
						uint num4 = ResourcePackManager.ComputeStringHash(k);
						bool flag65 = num4 > 1975626753U;
						bool flag66 = flag65;
						bool flag67 = flag66;
						bool flag68 = flag67;
						bool flag69 = flag68;
						bool flag70 = flag69;
						bool flag71 = flag70;
						if (flag71)
						{
							bool flag72 = num4 <= 3114320056U;
							bool flag73 = flag72;
							bool flag74 = flag73;
							bool flag75 = flag74;
							bool flag76 = flag75;
							bool flag77 = flag76;
							bool flag78 = flag77;
							if (flag78)
							{
								bool flag79 = num4 <= 2485350594U;
								bool flag80 = flag79;
								bool flag81 = flag80;
								bool flag82 = flag81;
								bool flag83 = flag82;
								bool flag84 = flag83;
								bool flag85 = flag84;
								if (flag85)
								{
									bool flag86 = num4 <= 2292892005U;
									bool flag87 = flag86;
									bool flag88 = flag87;
									bool flag89 = flag88;
									bool flag90 = flag89;
									bool flag91 = flag90;
									bool flag92 = flag91;
									if (flag92)
									{
										bool flag93 = num4 <= 2042787804U;
										bool flag94 = flag93;
										bool flag95 = flag94;
										bool flag96 = flag95;
										bool flag97 = flag96;
										bool flag98 = flag97;
										bool flag99 = flag98;
										if (flag99)
										{
											bool flag100 = num4 != 1984283208U;
											bool flag101 = flag100;
											bool flag102 = flag101;
											bool flag103 = flag102;
											bool flag104 = flag103;
											bool flag105 = flag104;
											bool flag106 = flag105;
											if (flag106)
											{
												bool flag107 = num4 != 2042787804U;
												bool flag108 = flag107;
												bool flag109 = flag108;
												bool flag110 = flag109;
												bool flag111 = flag110;
												bool flag112 = flag111;
												bool flag113 = flag112;
												if (flag113)
												{
													goto IL_57EE;
												}
												bool flag114 = !(k == "pathmarker");
												bool flag115 = flag114;
												bool flag116 = flag115;
												bool flag117 = flag116;
												bool flag118 = flag117;
												bool flag119 = flag118;
												bool flag120 = flag119;
												if (flag120)
												{
													goto IL_57EE;
												}
												goto IL_452C;
											}
											else
											{
												bool flag121 = !(k == "staminanumberknightonly");
												bool flag122 = flag121;
												bool flag123 = flag122;
												bool flag124 = flag123;
												bool flag125 = flag124;
												bool flag126 = flag125;
												bool flag127 = flag126;
												if (flag127)
												{
													goto IL_57EE;
												}
												goto IL_452C;
											}
										}
										else
										{
											bool flag128 = num4 != 2189550183U;
											bool flag129 = flag128;
											bool flag130 = flag129;
											bool flag131 = flag130;
											bool flag132 = flag131;
											bool flag133 = flag132;
											bool flag134 = flag133;
											if (flag134)
											{
												bool flag135 = num4 != 2292892005U;
												bool flag136 = flag135;
												bool flag137 = flag136;
												bool flag138 = flag137;
												bool flag139 = flag138;
												bool flag140 = flag139;
												bool flag141 = flag140;
												if (flag141)
												{
													goto IL_57EE;
												}
												bool flag142 = !(k == "regennumberenemy");
												bool flag143 = flag142;
												bool flag144 = flag143;
												bool flag145 = flag144;
												bool flag146 = flag145;
												bool flag147 = flag146;
												bool flag148 = flag147;
												if (flag148)
												{
													goto IL_57EE;
												}
												goto IL_452C;
											}
											else
											{
												bool flag149 = !(k == "bombmaplayers");
												bool flag150 = flag149;
												bool flag151 = flag150;
												bool flag152 = flag151;
												bool flag153 = flag152;
												bool flag154 = flag153;
												bool flag155 = flag154;
												if (flag155)
												{
													goto IL_57EE;
												}
												return true;
											}
										}
									}
									else
									{
										bool flag156 = num4 <= 2364795673U;
										bool flag157 = flag156;
										bool flag158 = flag157;
										bool flag159 = flag158;
										bool flag160 = flag159;
										bool flag161 = flag160;
										bool flag162 = flag161;
										if (flag162)
										{
											bool flag163 = num4 != 2351093842U;
											bool flag164 = flag163;
											bool flag165 = flag164;
											bool flag166 = flag165;
											bool flag167 = flag166;
											bool flag168 = flag167;
											bool flag169 = flag168;
											if (flag169)
											{
												bool flag170 = num4 != 2364795673U;
												bool flag171 = flag170;
												bool flag172 = flag171;
												bool flag173 = flag172;
												bool flag174 = flag173;
												bool flag175 = flag174;
												bool flag176 = flag175;
												if (flag176)
												{
													goto IL_57EE;
												}
												bool flag177 = !(k == "staminacolor");
												bool flag178 = flag177;
												bool flag179 = flag178;
												bool flag180 = flag179;
												bool flag181 = flag180;
												bool flag182 = flag181;
												bool flag183 = flag182;
												if (flag183)
												{
													goto IL_57EE;
												}
												goto IL_53B2;
											}
											else
											{
												bool flag184 = !(k == "pathtrail");
												bool flag185 = flag184;
												bool flag186 = flag185;
												bool flag187 = flag186;
												bool flag188 = flag187;
												bool flag189 = flag188;
												bool flag190 = flag189;
												if (flag190)
												{
													goto IL_57EE;
												}
												goto IL_452C;
											}
										}
										else
										{
											bool flag191 = num4 != 2386966301U;
											bool flag192 = flag191;
											bool flag193 = flag192;
											bool flag194 = flag193;
											bool flag195 = flag194;
											bool flag196 = flag195;
											bool flag197 = flag196;
											if (flag197)
											{
												bool flag198 = num4 != 2485350594U;
												bool flag199 = flag198;
												bool flag200 = flag199;
												bool flag201 = flag200;
												bool flag202 = flag201;
												bool flag203 = flag202;
												bool flag204 = flag203;
												if (flag204)
												{
													goto IL_57EE;
												}
												bool flag205 = !(k == "staminanumbercolor");
												bool flag206 = flag205;
												bool flag207 = flag206;
												bool flag208 = flag207;
												bool flag209 = flag208;
												bool flag210 = flag209;
												bool flag211 = flag210;
												if (flag211)
												{
													goto IL_57EE;
												}
											}
											else
											{
												bool flag212 = !(k == "pathenemycolor");
												bool flag213 = flag212;
												bool flag214 = flag213;
												bool flag215 = flag214;
												bool flag216 = flag215;
												bool flag217 = flag216;
												bool flag218 = flag217;
												if (flag218)
												{
													goto IL_57EE;
												}
												goto IL_4523;
											}
										}
									}
								}
								else
								{
									bool flag219 = num4 <= 2997847573U;
									bool flag220 = flag219;
									bool flag221 = flag220;
									bool flag222 = flag221;
									bool flag223 = flag222;
									bool flag224 = flag223;
									bool flag225 = flag224;
									if (flag225)
									{
										bool flag226 = num4 <= 2822884823U;
										bool flag227 = flag226;
										bool flag228 = flag227;
										bool flag229 = flag228;
										bool flag230 = flag229;
										bool flag231 = flag230;
										bool flag232 = flag231;
										if (flag232)
										{
											bool flag233 = num4 != 2701336191U;
											bool flag234 = flag233;
											bool flag235 = flag234;
											bool flag236 = flag235;
											bool flag237 = flag236;
											bool flag238 = flag237;
											bool flag239 = flag238;
											if (flag239)
											{
												bool flag240 = num4 != 2822884823U;
												bool flag241 = flag240;
												bool flag242 = flag241;
												bool flag243 = flag242;
												bool flag244 = flag243;
												bool flag245 = flag244;
												bool flag246 = flag245;
												if (flag246)
												{
													goto IL_57EE;
												}
												bool flag247 = !(k == "hpnumbercolor");
												bool flag248 = flag247;
												bool flag249 = flag248;
												bool flag250 = flag249;
												bool flag251 = flag250;
												bool flag252 = flag251;
												bool flag253 = flag252;
												if (flag253)
												{
													goto IL_57EE;
												}
											}
											else
											{
												bool flag254 = !(k == "statnumberhidefull");
												bool flag255 = flag254;
												bool flag256 = flag255;
												bool flag257 = flag256;
												bool flag258 = flag257;
												bool flag259 = flag258;
												bool flag260 = flag259;
												if (flag260)
												{
													goto IL_57EE;
												}
												goto IL_452C;
											}
										}
										else
										{
											bool flag261 = num4 != 2850409677U;
											bool flag262 = flag261;
											bool flag263 = flag262;
											bool flag264 = flag263;
											bool flag265 = flag264;
											bool flag266 = flag265;
											bool flag267 = flag266;
											if (flag267)
											{
												bool flag268 = num4 != 2997847573U;
												bool flag269 = flag268;
												bool flag270 = flag269;
												bool flag271 = flag270;
												bool flag272 = flag271;
												bool flag273 = flag272;
												bool flag274 = flag273;
												if (flag274)
												{
													goto IL_57EE;
												}
												bool flag275 = !(k == "buildingregennumberenemy");
												bool flag276 = flag275;
												bool flag277 = flag276;
												bool flag278 = flag277;
												bool flag279 = flag278;
												bool flag280 = flag279;
												bool flag281 = flag280;
												if (flag281)
												{
													goto IL_57EE;
												}
												goto IL_452C;
											}
											else
											{
												bool flag282 = !(k == "ammosecondcolorbrightness");
												bool flag283 = flag282;
												bool flag284 = flag283;
												bool flag285 = flag284;
												bool flag286 = flag285;
												bool flag287 = flag286;
												bool flag288 = flag287;
												if (flag288)
												{
													goto IL_57EE;
												}
												cfg.ammoSecondBrightness = ResourcePackManager.ParseHpFloat(val, cfg.ammoSecondBrightness, 0f, 4f);
												return true;
											}
										}
									}
									else
									{
										bool flag289 = num4 <= 3071413963U;
										bool flag290 = flag289;
										bool flag291 = flag290;
										bool flag292 = flag291;
										bool flag293 = flag292;
										bool flag294 = flag293;
										bool flag295 = flag294;
										if (flag295)
										{
											bool flag296 = num4 != 3054636344U;
											bool flag297 = flag296;
											bool flag298 = flag297;
											bool flag299 = flag298;
											bool flag300 = flag299;
											bool flag301 = flag300;
											bool flag302 = flag301;
											if (flag302)
											{
												bool flag303 = num4 != 3071413963U;
												bool flag304 = flag303;
												bool flag305 = flag304;
												bool flag306 = flag305;
												bool flag307 = flag306;
												bool flag308 = flag307;
												bool flag309 = flag308;
												if (flag309)
												{
													goto IL_57EE;
												}
												bool flag310 = !(k == "staminanumberoffsety");
												bool flag311 = flag310;
												bool flag312 = flag311;
												bool flag313 = flag312;
												bool flag314 = flag313;
												bool flag315 = flag314;
												bool flag316 = flag315;
												if (flag316)
												{
													goto IL_57EE;
												}
												cfg.stamNumY = ResourcePackManager.ParseHpFloat(val, cfg.stamNumY, -400f, 400f);
												return true;
											}
											else
											{
												bool flag317 = !(k == "staminanumberoffsetx");
												bool flag318 = flag317;
												bool flag319 = flag318;
												bool flag320 = flag319;
												bool flag321 = flag320;
												bool flag322 = flag321;
												bool flag323 = flag322;
												if (flag323)
												{
													goto IL_57EE;
												}
												cfg.stamNumX = ResourcePackManager.ParseHpFloat(val, cfg.stamNumX, -400f, 400f);
												return true;
											}
										}
										else
										{
											bool flag324 = num4 != 3106598185U;
											bool flag325 = flag324;
											bool flag326 = flag325;
											bool flag327 = flag326;
											bool flag328 = flag327;
											bool flag329 = flag328;
											bool flag330 = flag329;
											if (flag330)
											{
												bool flag331 = num4 != 3114320056U;
												bool flag332 = flag331;
												bool flag333 = flag332;
												bool flag334 = flag333;
												bool flag335 = flag334;
												bool flag336 = flag335;
												bool flag337 = flag336;
												if (flag337)
												{
													goto IL_57EE;
												}
												bool flag338 = !(k == "statnumberoutline");
												bool flag339 = flag338;
												bool flag340 = flag339;
												bool flag341 = flag340;
												bool flag342 = flag341;
												bool flag343 = flag342;
												bool flag344 = flag343;
												if (flag344)
												{
													goto IL_57EE;
												}
												goto IL_452C;
											}
											else
											{
												bool flag345 = !(k == "regennumberown");
												bool flag346 = flag345;
												bool flag347 = flag346;
												bool flag348 = flag347;
												bool flag349 = flag348;
												bool flag350 = flag349;
												bool flag351 = flag350;
												if (flag351)
												{
													goto IL_57EE;
												}
												goto IL_452C;
											}
										}
									}
								}
							}
							else
							{
								bool flag352 = num4 <= 3530897846U;
								bool flag353 = flag352;
								bool flag354 = flag353;
								bool flag355 = flag354;
								bool flag356 = flag355;
								bool flag357 = flag356;
								bool flag358 = flag357;
								if (flag358)
								{
									bool flag359 = num4 <= 3407698161U;
									bool flag360 = flag359;
									bool flag361 = flag360;
									bool flag362 = flag361;
									bool flag363 = flag362;
									bool flag364 = flag363;
									bool flag365 = flag364;
									if (flag365)
									{
										bool flag366 = num4 <= 3215304076U;
										bool flag367 = flag366;
										bool flag368 = flag367;
										bool flag369 = flag368;
										bool flag370 = flag369;
										bool flag371 = flag370;
										bool flag372 = flag371;
										if (flag372)
										{
											bool flag373 = num4 != 3122104833U;
											bool flag374 = flag373;
											bool flag375 = flag374;
											bool flag376 = flag375;
											bool flag377 = flag376;
											bool flag378 = flag377;
											bool flag379 = flag378;
											if (flag379)
											{
												bool flag380 = num4 != 3215304076U;
												bool flag381 = flag380;
												bool flag382 = flag381;
												bool flag383 = flag382;
												bool flag384 = flag383;
												bool flag385 = flag384;
												bool flag386 = flag385;
												if (flag386)
												{
													goto IL_57EE;
												}
												bool flag387 = !(k == "buildinghpnumberenemy");
												bool flag388 = flag387;
												bool flag389 = flag388;
												bool flag390 = flag389;
												bool flag391 = flag390;
												bool flag392 = flag391;
												bool flag393 = flag392;
												if (flag393)
												{
													goto IL_57EE;
												}
												goto IL_452C;
											}
											else
											{
												bool flag394 = !(k == "bombbydamage");
												bool flag395 = flag394;
												bool flag396 = flag395;
												bool flag397 = flag396;
												bool flag398 = flag397;
												bool flag399 = flag398;
												bool flag400 = flag399;
												if (flag400)
												{
													goto IL_57EE;
												}
												goto IL_452C;
											}
										}
										else
										{
											bool flag401 = num4 != 3402221505U;
											bool flag402 = flag401;
											bool flag403 = flag402;
											bool flag404 = flag403;
											bool flag405 = flag404;
											bool flag406 = flag405;
											bool flag407 = flag406;
											if (flag407)
											{
												bool flag408 = num4 != 3407698161U;
												bool flag409 = flag408;
												bool flag410 = flag409;
												bool flag411 = flag410;
												bool flag412 = flag411;
												bool flag413 = flag412;
												bool flag414 = flag413;
												if (flag414)
												{
													goto IL_57EE;
												}
												bool flag415 = !(k == "staminaknightonly");
												bool flag416 = flag415;
												bool flag417 = flag416;
												bool flag418 = flag417;
												bool flag419 = flag418;
												bool flag420 = flag419;
												bool flag421 = flag420;
												if (flag421)
												{
													goto IL_57EE;
												}
												goto IL_53B2;
											}
											else
											{
												bool flag422 = !(k == "pathowncolor");
												bool flag423 = flag422;
												bool flag424 = flag423;
												bool flag425 = flag424;
												bool flag426 = flag425;
												bool flag427 = flag426;
												bool flag428 = flag427;
												if (flag428)
												{
													goto IL_57EE;
												}
											}
										}
									}
									else
									{
										bool flag429 = num4 <= 3445320145U;
										bool flag430 = flag429;
										bool flag431 = flag430;
										bool flag432 = flag431;
										bool flag433 = flag432;
										bool flag434 = flag433;
										bool flag435 = flag434;
										if (flag435)
										{
											bool flag436 = num4 != 3444429209U;
											bool flag437 = flag436;
											bool flag438 = flag437;
											bool flag439 = flag438;
											bool flag440 = flag439;
											bool flag441 = flag440;
											bool flag442 = flag441;
											if (flag442)
											{
												bool flag443 = num4 != 3445320145U;
												bool flag444 = flag443;
												bool flag445 = flag444;
												bool flag446 = flag445;
												bool flag447 = flag446;
												bool flag448 = flag447;
												bool flag449 = flag448;
												if (flag449)
												{
													goto IL_57EE;
												}
												bool flag450 = !(k == "ammoring");
												bool flag451 = flag450;
												bool flag452 = flag451;
												bool flag453 = flag452;
												bool flag454 = flag453;
												bool flag455 = flag454;
												bool flag456 = flag455;
												if (flag456)
												{
													goto IL_57EE;
												}
												goto IL_53B2;
											}
											else
											{
												bool flag457 = !(k == "buildingregennumberown");
												bool flag458 = flag457;
												bool flag459 = flag458;
												bool flag460 = flag459;
												bool flag461 = flag460;
												bool flag462 = flag461;
												bool flag463 = flag462;
												if (flag463)
												{
													goto IL_57EE;
												}
												goto IL_452C;
											}
										}
										else
										{
											bool flag464 = num4 != 3459584005U;
											bool flag465 = flag464;
											bool flag466 = flag465;
											bool flag467 = flag466;
											bool flag468 = flag467;
											bool flag469 = flag468;
											bool flag470 = flag469;
											if (flag470)
											{
												bool flag471 = num4 != 3530897846U;
												bool flag472 = flag471;
												bool flag473 = flag472;
												bool flag474 = flag473;
												bool flag475 = flag474;
												bool flag476 = flag475;
												bool flag477 = flag476;
												if (flag477)
												{
													goto IL_57EE;
												}
												bool flag478 = !(k == "regennumbercolor");
												bool flag479 = flag478;
												bool flag480 = flag479;
												bool flag481 = flag480;
												bool flag482 = flag481;
												bool flag483 = flag482;
												bool flag484 = flag483;
												if (flag484)
												{
													goto IL_57EE;
												}
											}
											else
											{
												bool flag485 = !(k == "staminanumberown");
												bool flag486 = flag485;
												bool flag487 = flag486;
												bool flag488 = flag487;
												bool flag489 = flag488;
												bool flag490 = flag489;
												bool flag491 = flag490;
												if (flag491)
												{
													goto IL_57EE;
												}
												goto IL_452C;
											}
										}
									}
								}
								else
								{
									bool flag492 = num4 <= 4039293358U;
									bool flag493 = flag492;
									bool flag494 = flag493;
									bool flag495 = flag494;
									bool flag496 = flag495;
									bool flag497 = flag496;
									bool flag498 = flag497;
									if (flag498)
									{
										bool flag499 = num4 <= 3883417566U;
										bool flag500 = flag499;
										bool flag501 = flag500;
										bool flag502 = flag501;
										bool flag503 = flag502;
										bool flag504 = flag503;
										bool flag505 = flag504;
										if (flag505)
										{
											bool flag506 = num4 != 3859127737U;
											bool flag507 = flag506;
											bool flag508 = flag507;
											bool flag509 = flag508;
											bool flag510 = flag509;
											bool flag511 = flag510;
											bool flag512 = flag511;
											if (flag512)
											{
												bool flag513 = num4 != 3883417566U;
												bool flag514 = flag513;
												bool flag515 = flag514;
												bool flag516 = flag515;
												bool flag517 = flag516;
												bool flag518 = flag517;
												bool flag519 = flag518;
												if (flag519)
												{
													goto IL_57EE;
												}
												bool flag520 = !(k == "pathown");
												bool flag521 = flag520;
												bool flag522 = flag521;
												bool flag523 = flag522;
												bool flag524 = flag523;
												bool flag525 = flag524;
												bool flag526 = flag525;
												if (flag526)
												{
													goto IL_57EE;
												}
												goto IL_452C;
											}
											else
											{
												bool flag527 = !(k == "staminanumberbypalette");
												bool flag528 = flag527;
												bool flag529 = flag528;
												bool flag530 = flag529;
												bool flag531 = flag530;
												bool flag532 = flag531;
												bool flag533 = flag532;
												if (flag533)
												{
													goto IL_57EE;
												}
												goto IL_452C;
											}
										}
										else
										{
											bool flag534 = num4 != 4038135191U;
											bool flag535 = flag534;
											bool flag536 = flag535;
											bool flag537 = flag536;
											bool flag538 = flag537;
											bool flag539 = flag538;
											bool flag540 = flag539;
											if (flag540)
											{
												bool flag541 = num4 != 4039293358U;
												bool flag542 = flag541;
												bool flag543 = flag542;
												bool flag544 = flag543;
												bool flag545 = flag544;
												bool flag546 = flag545;
												bool flag547 = flag546;
												if (flag547)
												{
													goto IL_57EE;
												}
												bool flag548 = !(k == "ammonumber");
												bool flag549 = flag548;
												bool flag550 = flag549;
												bool flag551 = flag550;
												bool flag552 = flag551;
												bool flag553 = flag552;
												bool flag554 = flag553;
												if (flag554)
												{
													goto IL_57EE;
												}
												goto IL_53B2;
											}
											else
											{
												bool flag555 = !(k == "pathlift");
												bool flag556 = flag555;
												bool flag557 = flag556;
												bool flag558 = flag557;
												bool flag559 = flag558;
												bool flag560 = flag559;
												bool flag561 = flag560;
												if (flag561)
												{
													goto IL_57EE;
												}
												cfg.pathLift = ResourcePackManager.ParseHpFloat(val, cfg.pathLift, 0f, 3f);
												return true;
											}
										}
									}
									else
									{
										bool flag562 = num4 <= 4075668206U;
										bool flag563 = flag562;
										bool flag564 = flag563;
										bool flag565 = flag564;
										bool flag566 = flag565;
										bool flag567 = flag566;
										bool flag568 = flag567;
										if (flag568)
										{
											bool flag569 = num4 != 4039709424U;
											bool flag570 = flag569;
											bool flag571 = flag570;
											bool flag572 = flag571;
											bool flag573 = flag572;
											bool flag574 = flag573;
											bool flag575 = flag574;
											if (flag575)
											{
												bool flag576 = num4 != 4075668206U;
												bool flag577 = flag576;
												bool flag578 = flag577;
												bool flag579 = flag578;
												bool flag580 = flag579;
												bool flag581 = flag580;
												bool flag582 = flag581;
												if (flag582)
												{
													goto IL_57EE;
												}
												bool flag583 = !(k == "bombmapopacity");
												bool flag584 = flag583;
												bool flag585 = flag584;
												bool flag586 = flag585;
												bool flag587 = flag586;
												bool flag588 = flag587;
												bool flag589 = flag588;
												if (flag589)
												{
													goto IL_57EE;
												}
												cfg.bombMapAlpha = ResourcePackManager.ParseHpFloat(val, cfg.bombMapAlpha, 0f, 1f);
												return true;
											}
											else
											{
												bool flag590 = !(k == "hpnumberown");
												bool flag591 = flag590;
												bool flag592 = flag591;
												bool flag593 = flag592;
												bool flag594 = flag593;
												bool flag595 = flag594;
												bool flag596 = flag595;
												if (flag596)
												{
													goto IL_57EE;
												}
												goto IL_452C;
											}
										}
										else
										{
											bool flag597 = num4 != 4121928960U;
											bool flag598 = flag597;
											bool flag599 = flag598;
											bool flag600 = flag599;
											bool flag601 = flag600;
											bool flag602 = flag601;
											bool flag603 = flag602;
											if (flag603)
											{
												bool flag604 = num4 != 4242499580U;
												bool flag605 = flag604;
												bool flag606 = flag605;
												bool flag607 = flag606;
												bool flag608 = flag607;
												bool flag609 = flag608;
												bool flag610 = flag609;
												if (flag610)
												{
													goto IL_57EE;
												}
												bool flag611 = !(k == "hpnumberenemy");
												bool flag612 = flag611;
												bool flag613 = flag612;
												bool flag614 = flag613;
												bool flag615 = flag614;
												bool flag616 = flag615;
												bool flag617 = flag616;
												if (flag617)
												{
													goto IL_57EE;
												}
												goto IL_452C;
											}
											else
											{
												bool flag618 = !(k == "buildinghpnumberown");
												bool flag619 = flag618;
												bool flag620 = flag619;
												bool flag621 = flag620;
												bool flag622 = flag621;
												bool flag623 = flag622;
												bool flag624 = flag623;
												if (flag624)
												{
													goto IL_57EE;
												}
												goto IL_452C;
											}
										}
									}
								}
							}
							Color color2;
							bool flag625 = ResourcePackManager.TryParseHpColor(val, out color2);
							bool flag626 = flag625;
							bool flag627 = flag626;
							bool flag628 = flag627;
							bool flag629 = flag628;
							bool flag630 = flag629;
							bool flag631 = flag630;
							bool flag632 = flag631;
							if (flag632)
							{
								bool flag633 = k == "hpnumbercolor";
								bool flag634 = flag633;
								bool flag635 = flag634;
								bool flag636 = flag635;
								bool flag637 = flag636;
								bool flag638 = flag637;
								bool flag639 = flag638;
								bool flag640 = flag639;
								if (flag640)
								{
									cfg.hpNumColor = color2;
								}
								else
								{
									bool flag641 = k == "staminanumbercolor";
									bool flag642 = flag641;
									bool flag643 = flag642;
									bool flag644 = flag643;
									bool flag645 = flag644;
									bool flag646 = flag645;
									bool flag647 = flag646;
									bool flag648 = flag647;
									if (flag648)
									{
										cfg.stamNumColor = color2;
									}
									else
									{
										bool flag649 = k == "regennumbercolor";
										bool flag650 = flag649;
										bool flag651 = flag650;
										bool flag652 = flag651;
										bool flag653 = flag652;
										bool flag654 = flag653;
										bool flag655 = flag654;
										bool flag656 = flag655;
										if (flag656)
										{
											cfg.regenNumColor = color2;
										}
										else
										{
											cfg.pathOwnColor = color2;
										}
									}
								}
							}
							else
							{
								problems.Add(string.Concat(new object[]
								{
									"line ",
									lineIdx + 1,
									": bad color '",
									val,
									"'"
								}));
							}
							return true;
						}
						bool flag657 = num4 <= 730900211U;
						bool flag658 = flag657;
						bool flag659 = flag658;
						bool flag660 = flag659;
						bool flag661 = flag660;
						bool flag662 = flag661;
						bool flag663 = flag662;
						if (flag663)
						{
							bool flag664 = num4 <= 361632960U;
							bool flag665 = flag664;
							bool flag666 = flag665;
							bool flag667 = flag666;
							bool flag668 = flag667;
							bool flag669 = flag668;
							bool flag670 = flag669;
							if (flag670)
							{
								bool flag671 = num4 <= 254053295U;
								bool flag672 = flag671;
								bool flag673 = flag672;
								bool flag674 = flag673;
								bool flag675 = flag674;
								bool flag676 = flag675;
								bool flag677 = flag676;
								if (flag677)
								{
									bool flag678 = num4 <= 103139007U;
									bool flag679 = flag678;
									bool flag680 = flag679;
									bool flag681 = flag680;
									bool flag682 = flag681;
									bool flag683 = flag682;
									bool flag684 = flag683;
									if (flag684)
									{
										bool flag685 = num4 != 66212653U;
										bool flag686 = flag685;
										bool flag687 = flag686;
										bool flag688 = flag687;
										bool flag689 = flag688;
										bool flag690 = flag689;
										bool flag691 = flag690;
										if (flag691)
										{
											bool flag692 = num4 != 103139007U;
											bool flag693 = flag692;
											bool flag694 = flag693;
											bool flag695 = flag694;
											bool flag696 = flag695;
											bool flag697 = flag696;
											bool flag698 = flag697;
											if (flag698)
											{
												goto IL_57EE;
											}
											bool flag699 = !(k == "pathopacity");
											bool flag700 = flag699;
											bool flag701 = flag700;
											bool flag702 = flag701;
											bool flag703 = flag702;
											bool flag704 = flag703;
											bool flag705 = flag704;
											if (flag705)
											{
												goto IL_57EE;
											}
											cfg.pathAlpha = ResourcePackManager.ParseHpFloat(val, cfg.pathAlpha, 0.1f, 1f);
											return true;
										}
										else
										{
											bool flag706 = !(k == "bombmap");
											bool flag707 = flag706;
											bool flag708 = flag707;
											bool flag709 = flag708;
											bool flag710 = flag709;
											bool flag711 = flag710;
											bool flag712 = flag711;
											if (flag712)
											{
												goto IL_57EE;
											}
											goto IL_452C;
										}
									}
									else
									{
										bool flag713 = num4 != 204983391U;
										bool flag714 = flag713;
										bool flag715 = flag714;
										bool flag716 = flag715;
										bool flag717 = flag716;
										bool flag718 = flag717;
										bool flag719 = flag718;
										if (flag719)
										{
											bool flag720 = num4 != 254053295U;
											bool flag721 = flag720;
											bool flag722 = flag721;
											bool flag723 = flag722;
											bool flag724 = flag723;
											bool flag725 = flag724;
											bool flag726 = flag725;
											if (flag726)
											{
												goto IL_57EE;
											}
											bool flag727 = !(k == "pathpredict");
											bool flag728 = flag727;
											bool flag729 = flag728;
											bool flag730 = flag729;
											bool flag731 = flag730;
											bool flag732 = flag731;
											bool flag733 = flag732;
											if (flag733)
											{
												goto IL_57EE;
											}
											goto IL_452C;
										}
										else
										{
											bool flag734 = !(k == "statnumberopacity");
											bool flag735 = flag734;
											bool flag736 = flag735;
											bool flag737 = flag736;
											bool flag738 = flag737;
											bool flag739 = flag738;
											bool flag740 = flag739;
											if (flag740)
											{
												goto IL_57EE;
											}
											cfg.statAlpha = ResourcePackManager.ParseHpFloat(val, cfg.statAlpha, 0.1f, 1f);
											return true;
										}
									}
								}
								else
								{
									bool flag741 = num4 <= 327096419U;
									bool flag742 = flag741;
									bool flag743 = flag742;
									bool flag744 = flag743;
									bool flag745 = flag744;
									bool flag746 = flag745;
									bool flag747 = flag746;
									if (flag747)
									{
										bool flag748 = num4 != 300435097U;
										bool flag749 = flag748;
										bool flag750 = flag749;
										bool flag751 = flag750;
										bool flag752 = flag751;
										bool flag753 = flag752;
										bool flag754 = flag753;
										if (flag754)
										{
											bool flag755 = num4 != 327096419U;
											bool flag756 = flag755;
											bool flag757 = flag756;
											bool flag758 = flag757;
											bool flag759 = flag758;
											bool flag760 = flag759;
											bool flag761 = flag760;
											if (flag761)
											{
												goto IL_57EE;
											}
											bool flag762 = !(k == "statnumberhidefar");
											bool flag763 = flag762;
											bool flag764 = flag763;
											bool flag765 = flag764;
											bool flag766 = flag765;
											bool flag767 = flag766;
											bool flag768 = flag767;
											if (flag768)
											{
												goto IL_57EE;
											}
											goto IL_452C;
										}
										else
										{
											bool flag769 = !(k == "ammonumberbold");
											bool flag770 = flag769;
											bool flag771 = flag770;
											bool flag772 = flag771;
											bool flag773 = flag772;
											bool flag774 = flag773;
											bool flag775 = flag774;
											if (flag775)
											{
												goto IL_57EE;
											}
											goto IL_53B2;
										}
									}
									else
									{
										bool flag776 = num4 != 338614673U;
										bool flag777 = flag776;
										bool flag778 = flag777;
										bool flag779 = flag778;
										bool flag780 = flag779;
										bool flag781 = flag780;
										bool flag782 = flag781;
										if (flag782)
										{
											bool flag783 = num4 != 361632960U;
											bool flag784 = flag783;
											bool flag785 = flag784;
											bool flag786 = flag785;
											bool flag787 = flag786;
											bool flag788 = flag787;
											bool flag789 = flag788;
											if (flag789)
											{
												goto IL_57EE;
											}
											bool flag790 = !(k == "ammonumberoutline");
											bool flag791 = flag790;
											bool flag792 = flag791;
											bool flag793 = flag792;
											bool flag794 = flag793;
											bool flag795 = flag794;
											bool flag796 = flag795;
											if (flag796)
											{
												goto IL_57EE;
											}
											goto IL_53B2;
										}
										else
										{
											bool flag797 = !(k == "ammonumbercolor");
											bool flag798 = flag797;
											bool flag799 = flag798;
											bool flag800 = flag799;
											bool flag801 = flag800;
											bool flag802 = flag801;
											bool flag803 = flag802;
											if (flag803)
											{
												goto IL_57EE;
											}
											Color numColor;
											bool flag804 = ResourcePackManager.TryParseHpColor(val, out numColor);
											bool flag805 = flag804;
											bool flag806 = flag805;
											bool flag807 = flag806;
											bool flag808 = flag807;
											bool flag809 = flag808;
											bool flag810 = flag809;
											bool flag811 = flag810;
											if (flag811)
											{
												cfg.numColor = numColor;
											}
											else
											{
												problems.Add(string.Concat(new object[]
												{
													"line ",
													lineIdx + 1,
													": bad color '",
													val,
													"'"
												}));
											}
											return true;
										}
									}
								}
							}
							else
							{
								bool flag812 = num4 <= 563610350U;
								bool flag813 = flag812;
								bool flag814 = flag813;
								bool flag815 = flag814;
								bool flag816 = flag815;
								bool flag817 = flag816;
								bool flag818 = flag817;
								if (flag818)
								{
									bool flag819 = num4 <= 417110146U;
									bool flag820 = flag819;
									bool flag821 = flag820;
									bool flag822 = flag821;
									bool flag823 = flag822;
									bool flag824 = flag823;
									bool flag825 = flag824;
									if (flag825)
									{
										bool flag826 = num4 != 376439023U;
										bool flag827 = flag826;
										bool flag828 = flag827;
										bool flag829 = flag828;
										bool flag830 = flag829;
										bool flag831 = flag830;
										bool flag832 = flag831;
										if (flag832)
										{
											bool flag833 = num4 != 417110146U;
											bool flag834 = flag833;
											bool flag835 = flag834;
											bool flag836 = flag835;
											bool flag837 = flag836;
											bool flag838 = flag837;
											bool flag839 = flag838;
											if (flag839)
											{
												goto IL_57EE;
											}
											bool flag840 = !(k == "pathwidth");
											bool flag841 = flag840;
											bool flag842 = flag841;
											bool flag843 = flag842;
											bool flag844 = flag843;
											bool flag845 = flag844;
											bool flag846 = flag845;
											if (flag846)
											{
												goto IL_57EE;
											}
											cfg.pathWidth = ResourcePackManager.ParseHpFloat(val, cfg.pathWidth, 1f, 20f);
											return true;
										}
										else
										{
											bool flag847 = !(k == "ammonumbersize");
											bool flag848 = flag847;
											bool flag849 = flag848;
											bool flag850 = flag849;
											bool flag851 = flag850;
											bool flag852 = flag851;
											bool flag853 = flag852;
											if (flag853)
											{
												goto IL_57EE;
											}
											cfg.numSize = ResourcePackManager.ParseHpFloat(val, cfg.numSize, 0.3f, 5f);
											return true;
										}
									}
									else
									{
										bool flag854 = num4 != 437145137U;
										bool flag855 = flag854;
										bool flag856 = flag855;
										bool flag857 = flag856;
										bool flag858 = flag857;
										bool flag859 = flag858;
										bool flag860 = flag859;
										if (flag860)
										{
											bool flag861 = num4 != 563610350U;
											bool flag862 = flag861;
											bool flag863 = flag862;
											bool flag864 = flag863;
											bool flag865 = flag864;
											bool flag866 = flag865;
											bool flag867 = flag866;
											if (flag867)
											{
												goto IL_57EE;
											}
											bool flag868 = !(k == "pathontop");
											bool flag869 = flag868;
											bool flag870 = flag869;
											bool flag871 = flag870;
											bool flag872 = flag871;
											bool flag873 = flag872;
											bool flag874 = flag873;
											if (flag874)
											{
												goto IL_57EE;
											}
											goto IL_452C;
										}
										else
										{
											bool flag875 = !(k == "pathselectedonly");
											bool flag876 = flag875;
											bool flag877 = flag876;
											bool flag878 = flag877;
											bool flag879 = flag878;
											bool flag880 = flag879;
											bool flag881 = flag880;
											if (flag881)
											{
												goto IL_57EE;
											}
											goto IL_452C;
										}
									}
								}
								else
								{
									bool flag882 = num4 <= 639905408U;
									bool flag883 = flag882;
									bool flag884 = flag883;
									bool flag885 = flag884;
									bool flag886 = flag885;
									bool flag887 = flag886;
									bool flag888 = flag887;
									if (flag888)
									{
										bool flag889 = num4 != 585466046U;
										bool flag890 = flag889;
										bool flag891 = flag890;
										bool flag892 = flag891;
										bool flag893 = flag892;
										bool flag894 = flag893;
										bool flag895 = flag894;
										if (flag895)
										{
											bool flag896 = num4 != 639905408U;
											bool flag897 = flag896;
											bool flag898 = flag897;
											bool flag899 = flag898;
											bool flag900 = flag899;
											bool flag901 = flag900;
											bool flag902 = flag901;
											if (flag902)
											{
												goto IL_57EE;
											}
											bool flag903 = !(k == "selectedshift");
											bool flag904 = flag903;
											bool flag905 = flag904;
											bool flag906 = flag905;
											bool flag907 = flag906;
											bool flag908 = flag907;
											bool flag909 = flag908;
											if (flag909)
											{
												goto IL_57EE;
											}
											cfg.selShift = ResourcePackManager.ParseHpFloat(val, cfg.selShift, -0.6f, 0.6f);
											return true;
										}
										else
										{
											bool flag910 = !(k == "ammonumbercolorbyammo");
											bool flag911 = flag910;
											bool flag912 = flag911;
											bool flag913 = flag912;
											bool flag914 = flag913;
											bool flag915 = flag914;
											bool flag916 = flag915;
											if (flag916)
											{
												goto IL_57EE;
											}
											goto IL_53B2;
										}
									}
									else
									{
										bool flag917 = num4 != 714122592U;
										bool flag918 = flag917;
										bool flag919 = flag918;
										bool flag920 = flag919;
										bool flag921 = flag920;
										bool flag922 = flag921;
										bool flag923 = flag922;
										if (flag923)
										{
											bool flag924 = num4 != 730900211U;
											bool flag925 = flag924;
											bool flag926 = flag925;
											bool flag927 = flag926;
											bool flag928 = flag927;
											bool flag929 = flag928;
											bool flag930 = flag929;
											if (flag930)
											{
												goto IL_57EE;
											}
											bool flag931 = !(k == "ammonumberoffsetx");
											bool flag932 = flag931;
											bool flag933 = flag932;
											bool flag934 = flag933;
											bool flag935 = flag934;
											bool flag936 = flag935;
											bool flag937 = flag936;
											if (flag937)
											{
												goto IL_57EE;
											}
											cfg.numX = ResourcePackManager.ParseHpFloat(val, cfg.numX, -400f, 400f);
											return true;
										}
										else
										{
											bool flag938 = !(k == "ammonumberoffsety");
											bool flag939 = flag938;
											bool flag940 = flag939;
											bool flag941 = flag940;
											bool flag942 = flag941;
											bool flag943 = flag942;
											bool flag944 = flag943;
											if (flag944)
											{
												goto IL_57EE;
											}
											cfg.numY = ResourcePackManager.ParseHpFloat(val, cfg.numY, -400f, 400f);
											return true;
										}
									}
								}
							}
						}
						else
						{
							bool flag945 = num4 <= 1204213182U;
							bool flag946 = flag945;
							bool flag947 = flag946;
							bool flag948 = flag947;
							bool flag949 = flag948;
							bool flag950 = flag949;
							bool flag951 = flag950;
							if (flag951)
							{
								bool flag952 = num4 <= 931630888U;
								bool flag953 = flag952;
								bool flag954 = flag953;
								bool flag955 = flag954;
								bool flag956 = flag955;
								bool flag957 = flag956;
								bool flag958 = flag957;
								if (flag958)
								{
									bool flag959 = num4 <= 799968186U;
									bool flag960 = flag959;
									bool flag961 = flag960;
									bool flag962 = flag961;
									bool flag963 = flag962;
									bool flag964 = flag963;
									bool flag965 = flag964;
									if (flag965)
									{
										bool flag966 = num4 != 783290378U;
										bool flag967 = flag966;
										bool flag968 = flag967;
										bool flag969 = flag968;
										bool flag970 = flag969;
										bool flag971 = flag970;
										bool flag972 = flag971;
										if (flag972)
										{
											bool flag973 = num4 != 799968186U;
											bool flag974 = flag973;
											bool flag975 = flag974;
											bool flag976 = flag975;
											bool flag977 = flag976;
											bool flag978 = flag977;
											bool flag979 = flag978;
											if (flag979)
											{
												goto IL_57EE;
											}
											bool flag980 = !(k == "bombmapallnumbers");
											bool flag981 = flag980;
											bool flag982 = flag981;
											bool flag983 = flag982;
											bool flag984 = flag983;
											bool flag985 = flag984;
											bool flag986 = flag985;
											if (flag986)
											{
												goto IL_57EE;
											}
											goto IL_452C;
										}
										else
										{
											bool flag987 = !(k == "pathenemy");
											bool flag988 = flag987;
											bool flag989 = flag988;
											bool flag990 = flag989;
											bool flag991 = flag990;
											bool flag992 = flag991;
											bool flag993 = flag992;
											if (flag993)
											{
												goto IL_57EE;
											}
										}
									}
									else
									{
										bool flag994 = num4 != 855441345U;
										bool flag995 = flag994;
										bool flag996 = flag995;
										bool flag997 = flag996;
										bool flag998 = flag997;
										bool flag999 = flag998;
										bool flag1000 = flag999;
										if (flag1000)
										{
											bool flag1001 = num4 != 931630888U;
											bool flag1002 = flag1001;
											bool flag1003 = flag1002;
											bool flag1004 = flag1003;
											bool flag1005 = flag1004;
											bool flag1006 = flag1005;
											bool flag1007 = flag1006;
											if (flag1007)
											{
												goto IL_57EE;
											}
											bool flag1008 = !(k == "hpnumberbypalette");
											bool flag1009 = flag1008;
											bool flag1010 = flag1009;
											bool flag1011 = flag1010;
											bool flag1012 = flag1011;
											bool flag1013 = flag1012;
											bool flag1014 = flag1013;
											if (flag1014)
											{
												goto IL_57EE;
											}
											goto IL_452C;
										}
										else
										{
											bool flag1015 = !(k == "statnumberbold");
											bool flag1016 = flag1015;
											bool flag1017 = flag1016;
											bool flag1018 = flag1017;
											bool flag1019 = flag1018;
											bool flag1020 = flag1019;
											bool flag1021 = flag1020;
											if (flag1021)
											{
												goto IL_57EE;
											}
											goto IL_452C;
										}
									}
								}
								else
								{
									bool flag1022 = num4 <= 992084225U;
									bool flag1023 = flag1022;
									bool flag1024 = flag1023;
									bool flag1025 = flag1024;
									bool flag1026 = flag1025;
									bool flag1027 = flag1026;
									bool flag1028 = flag1027;
									if (flag1028)
									{
										bool flag1029 = num4 != 989122441U;
										bool flag1030 = flag1029;
										bool flag1031 = flag1030;
										bool flag1032 = flag1031;
										bool flag1033 = flag1032;
										bool flag1034 = flag1033;
										bool flag1035 = flag1034;
										if (flag1035)
										{
											bool flag1036 = num4 != 992084225U;
											bool flag1037 = flag1036;
											bool flag1038 = flag1037;
											bool flag1039 = flag1038;
											bool flag1040 = flag1039;
											bool flag1041 = flag1040;
											bool flag1042 = flag1041;
											if (flag1042)
											{
												goto IL_57EE;
											}
											bool flag1043 = !(k == "bombmapnumbers");
											bool flag1044 = flag1043;
											bool flag1045 = flag1044;
											bool flag1046 = flag1045;
											bool flag1047 = flag1046;
											bool flag1048 = flag1047;
											bool flag1049 = flag1048;
											if (flag1049)
											{
												goto IL_57EE;
											}
											goto IL_452C;
										}
										else
										{
											bool flag1050 = !(k == "staminanumberenemy");
											bool flag1051 = flag1050;
											bool flag1052 = flag1051;
											bool flag1053 = flag1052;
											bool flag1054 = flag1053;
											bool flag1055 = flag1054;
											bool flag1056 = flag1055;
											if (flag1056)
											{
												goto IL_57EE;
											}
											goto IL_452C;
										}
									}
									else
									{
										bool flag1057 = num4 != 1160591909U;
										bool flag1058 = flag1057;
										bool flag1059 = flag1058;
										bool flag1060 = flag1059;
										bool flag1061 = flag1060;
										bool flag1062 = flag1061;
										bool flag1063 = flag1062;
										if (flag1063)
										{
											bool flag1064 = num4 != 1204213182U;
											bool flag1065 = flag1064;
											bool flag1066 = flag1065;
											bool flag1067 = flag1066;
											bool flag1068 = flag1067;
											bool flag1069 = flag1068;
											bool flag1070 = flag1069;
											if (flag1070)
											{
												goto IL_57EE;
											}
											bool flag1071 = !(k == "ammocolor");
											bool flag1072 = flag1071;
											bool flag1073 = flag1072;
											bool flag1074 = flag1073;
											bool flag1075 = flag1074;
											bool flag1076 = flag1075;
											bool flag1077 = flag1076;
											if (flag1077)
											{
												goto IL_57EE;
											}
											goto IL_53B2;
										}
										else
										{
											bool flag1078 = !(k == "statnumberformat");
											bool flag1079 = flag1078;
											bool flag1080 = flag1079;
											bool flag1081 = flag1080;
											bool flag1082 = flag1081;
											bool flag1083 = flag1082;
											bool flag1084 = flag1083;
											if (flag1084)
											{
												goto IL_57EE;
											}
											cfg.statFormat = Mathf.Clamp(Mathf.RoundToInt(ResourcePackManager.ParseHpFloat(val, (float)cfg.statFormat, 0f, 2f)), 0, 2);
											return true;
										}
									}
								}
							}
							else
							{
								bool flag1085 = num4 <= 1589947208U;
								bool flag1086 = flag1085;
								bool flag1087 = flag1086;
								bool flag1088 = flag1087;
								bool flag1089 = flag1088;
								bool flag1090 = flag1089;
								bool flag1091 = flag1090;
								if (flag1091)
								{
									bool flag1092 = num4 <= 1551092514U;
									bool flag1093 = flag1092;
									bool flag1094 = flag1093;
									bool flag1095 = flag1094;
									bool flag1096 = flag1095;
									bool flag1097 = flag1096;
									bool flag1098 = flag1097;
									if (flag1098)
									{
										bool flag1099 = num4 != 1480367098U;
										bool flag1100 = flag1099;
										bool flag1101 = flag1100;
										bool flag1102 = flag1101;
										bool flag1103 = flag1102;
										bool flag1104 = flag1103;
										bool flag1105 = flag1104;
										if (flag1105)
										{
											bool flag1106 = num4 != 1551092514U;
											bool flag1107 = flag1106;
											bool flag1108 = flag1107;
											bool flag1109 = flag1108;
											bool flag1110 = flag1109;
											bool flag1111 = flag1110;
											bool flag1112 = flag1111;
											if (flag1112)
											{
												goto IL_57EE;
											}
											bool flag1113 = !(k == "bombmapoffset");
											bool flag1114 = flag1113;
											bool flag1115 = flag1114;
											bool flag1116 = flag1115;
											bool flag1117 = flag1116;
											bool flag1118 = flag1117;
											bool flag1119 = flag1118;
											if (flag1119)
											{
												goto IL_57EE;
											}
											cfg.bombMapYOffset = ResourcePackManager.ParseHpFloat(val, cfg.bombMapYOffset, -3f, 6f);
											return true;
										}
										else
										{
											bool flag1120 = !(k == "bombmap3d");
											bool flag1121 = flag1120;
											bool flag1122 = flag1121;
											bool flag1123 = flag1122;
											bool flag1124 = flag1123;
											bool flag1125 = flag1124;
											bool flag1126 = flag1125;
											if (flag1126)
											{
												goto IL_57EE;
											}
											goto IL_452C;
										}
									}
									else
									{
										bool flag1127 = num4 != 1588829783U;
										bool flag1128 = flag1127;
										bool flag1129 = flag1128;
										bool flag1130 = flag1129;
										bool flag1131 = flag1130;
										bool flag1132 = flag1131;
										bool flag1133 = flag1132;
										if (flag1133)
										{
											bool flag1134 = num4 != 1589947208U;
											bool flag1135 = flag1134;
											bool flag1136 = flag1135;
											bool flag1137 = flag1136;
											bool flag1138 = flag1137;
											bool flag1139 = flag1138;
											bool flag1140 = flag1139;
											if (flag1140)
											{
												goto IL_57EE;
											}
											bool flag1141 = !(k == "buildingcolor");
											bool flag1142 = flag1141;
											bool flag1143 = flag1142;
											bool flag1144 = flag1143;
											bool flag1145 = flag1144;
											bool flag1146 = flag1145;
											bool flag1147 = flag1146;
											if (flag1147)
											{
												goto IL_57EE;
											}
											goto IL_452C;
										}
										else
										{
											bool flag1148 = !(k == "statnumbersize");
											bool flag1149 = flag1148;
											bool flag1150 = flag1149;
											bool flag1151 = flag1150;
											bool flag1152 = flag1151;
											bool flag1153 = flag1152;
											bool flag1154 = flag1153;
											if (flag1154)
											{
												goto IL_57EE;
											}
											cfg.statSize = ResourcePackManager.ParseHpFloat(val, cfg.statSize, 0.3f, 5f);
											return true;
										}
									}
								}
								else
								{
									bool flag1155 = num4 <= 1750982401U;
									bool flag1156 = flag1155;
									bool flag1157 = flag1156;
									bool flag1158 = flag1157;
									bool flag1159 = flag1158;
									bool flag1160 = flag1159;
									bool flag1161 = flag1160;
									if (flag1161)
									{
										bool flag1162 = num4 != 1734204782U;
										bool flag1163 = flag1162;
										bool flag1164 = flag1163;
										bool flag1165 = flag1164;
										bool flag1166 = flag1165;
										bool flag1167 = flag1166;
										bool flag1168 = flag1167;
										if (flag1168)
										{
											bool flag1169 = num4 != 1750982401U;
											bool flag1170 = flag1169;
											bool flag1171 = flag1170;
											bool flag1172 = flag1171;
											bool flag1173 = flag1172;
											bool flag1174 = flag1173;
											bool flag1175 = flag1174;
											if (flag1175)
											{
												goto IL_57EE;
											}
											bool flag1176 = !(k == "hpnumberoffsetx");
											bool flag1177 = flag1176;
											bool flag1178 = flag1177;
											bool flag1179 = flag1178;
											bool flag1180 = flag1179;
											bool flag1181 = flag1180;
											bool flag1182 = flag1181;
											if (flag1182)
											{
												goto IL_57EE;
											}
											cfg.hpNumX = ResourcePackManager.ParseHpFloat(val, cfg.hpNumX, -400f, 400f);
											return true;
										}
										else
										{
											bool flag1183 = !(k == "hpnumberoffsety");
											bool flag1184 = flag1183;
											bool flag1185 = flag1184;
											bool flag1186 = flag1185;
											bool flag1187 = flag1186;
											bool flag1188 = flag1187;
											bool flag1189 = flag1188;
											if (flag1189)
											{
												goto IL_57EE;
											}
											cfg.hpNumY = ResourcePackManager.ParseHpFloat(val, cfg.hpNumY, -400f, 400f);
											return true;
										}
									}
									else
									{
										bool flag1190 = num4 != 1913799322U;
										bool flag1191 = flag1190;
										bool flag1192 = flag1191;
										bool flag1193 = flag1192;
										bool flag1194 = flag1193;
										bool flag1195 = flag1194;
										bool flag1196 = flag1195;
										if (flag1196)
										{
											bool flag1197 = num4 != 1975626753U;
											bool flag1198 = flag1197;
											bool flag1199 = flag1198;
											bool flag1200 = flag1199;
											bool flag1201 = flag1200;
											bool flag1202 = flag1201;
											bool flag1203 = flag1202;
											if (flag1203)
											{
												goto IL_57EE;
											}
											bool flag1204 = !(k == "selectedshiftenabled");
											bool flag1205 = flag1204;
											bool flag1206 = flag1205;
											bool flag1207 = flag1206;
											bool flag1208 = flag1207;
											bool flag1209 = flag1208;
											bool flag1210 = flag1209;
											if (flag1210)
											{
												goto IL_57EE;
											}
											goto IL_53B2;
										}
										else
										{
											bool flag1211 = !(k == "staminasecondcolorbrightness");
											bool flag1212 = flag1211;
											bool flag1213 = flag1212;
											bool flag1214 = flag1213;
											bool flag1215 = flag1214;
											bool flag1216 = flag1215;
											bool flag1217 = flag1216;
											if (flag1217)
											{
												goto IL_57EE;
											}
											cfg.stamSecondBrightness = ResourcePackManager.ParseHpFloat(val, cfg.stamSecondBrightness, 0f, 4f);
											return true;
										}
									}
								}
							}
						}
						IL_4523:
						return true;
						IL_452C:
						bool flag1219;
						bool flag1218 = !ResourcePackManager.TryParseHpBool(val, out flag1219);
						bool flag1220 = flag1218;
						bool flag1221 = flag1220;
						bool flag1222 = flag1221;
						bool flag1223 = flag1222;
						bool flag1224 = flag1223;
						bool flag1225 = flag1224;
						bool flag1226 = flag1225;
						if (flag1226)
						{
							problems.Add(string.Concat(new object[]
							{
								"line ",
								lineIdx + 1,
								": ",
								key,
								" must be true/false"
							}));
							return true;
						}
						bool flag1227 = k == "hpnumberown";
						bool flag1228 = flag1227;
						bool flag1229 = flag1228;
						bool flag1230 = flag1229;
						bool flag1231 = flag1230;
						bool flag1232 = flag1231;
						bool flag1233 = flag1232;
						bool flag1234 = flag1233;
						if (flag1234)
						{
							cfg.hpNumOwn = flag1219;
						}
						else
						{
							bool flag1235 = k == "hpnumberenemy";
							bool flag1236 = flag1235;
							bool flag1237 = flag1236;
							bool flag1238 = flag1237;
							bool flag1239 = flag1238;
							bool flag1240 = flag1239;
							bool flag1241 = flag1240;
							bool flag1242 = flag1241;
							if (flag1242)
							{
								cfg.hpNumEnemy = flag1219;
							}
							else
							{
								bool flag1243 = k == "staminanumberown";
								bool flag1244 = flag1243;
								bool flag1245 = flag1244;
								bool flag1246 = flag1245;
								bool flag1247 = flag1246;
								bool flag1248 = flag1247;
								bool flag1249 = flag1248;
								bool flag1250 = flag1249;
								if (flag1250)
								{
									cfg.stamNumOwn = flag1219;
								}
								else
								{
									bool flag1251 = k == "staminanumberenemy";
									bool flag1252 = flag1251;
									bool flag1253 = flag1252;
									bool flag1254 = flag1253;
									bool flag1255 = flag1254;
									bool flag1256 = flag1255;
									bool flag1257 = flag1256;
									bool flag1258 = flag1257;
									if (flag1258)
									{
										cfg.stamNumEnemy = flag1219;
									}
									else
									{
										bool flag1259 = k == "staminanumberknightonly";
										bool flag1260 = flag1259;
										bool flag1261 = flag1260;
										bool flag1262 = flag1261;
										bool flag1263 = flag1262;
										bool flag1264 = flag1263;
										bool flag1265 = flag1264;
										bool flag1266 = flag1265;
										if (flag1266)
										{
											cfg.stamNumKnightOnly = flag1219;
										}
										else
										{
											bool flag1267 = k == "statnumberhidefull";
											bool flag1268 = flag1267;
											bool flag1269 = flag1268;
											bool flag1270 = flag1269;
											bool flag1271 = flag1270;
											bool flag1272 = flag1271;
											bool flag1273 = flag1272;
											bool flag1274 = flag1273;
											if (flag1274)
											{
												cfg.statHideFull = flag1219;
											}
											else
											{
												bool flag1275 = k == "statnumberhidefar";
												bool flag1276 = flag1275;
												bool flag1277 = flag1276;
												bool flag1278 = flag1277;
												bool flag1279 = flag1278;
												bool flag1280 = flag1279;
												bool flag1281 = flag1280;
												bool flag1282 = flag1281;
												if (flag1282)
												{
													cfg.statHideFar = flag1219;
												}
												else
												{
													bool flag1283 = k == "regennumberown";
													bool flag1284 = flag1283;
													bool flag1285 = flag1284;
													bool flag1286 = flag1285;
													bool flag1287 = flag1286;
													bool flag1288 = flag1287;
													bool flag1289 = flag1288;
													bool flag1290 = flag1289;
													if (flag1290)
													{
														cfg.regenNumOwn = flag1219;
													}
													else
													{
														bool flag1291 = k == "buildingcolor";
														bool flag1292 = flag1291;
														bool flag1293 = flag1292;
														bool flag1294 = flag1293;
														bool flag1295 = flag1294;
														bool flag1296 = flag1295;
														bool flag1297 = flag1296;
														bool flag1298 = flag1297;
														if (flag1298)
														{
															cfg.bldColor = flag1219;
														}
														else
														{
															bool flag1299 = k == "buildinghpnumberown";
															bool flag1300 = flag1299;
															bool flag1301 = flag1300;
															bool flag1302 = flag1301;
															bool flag1303 = flag1302;
															bool flag1304 = flag1303;
															bool flag1305 = flag1304;
															bool flag1306 = flag1305;
															if (flag1306)
															{
																cfg.bldHpNumOwn = flag1219;
															}
															else
															{
																bool flag1307 = k == "buildinghpnumberenemy";
																bool flag1308 = flag1307;
																bool flag1309 = flag1308;
																bool flag1310 = flag1309;
																bool flag1311 = flag1310;
																bool flag1312 = flag1311;
																bool flag1313 = flag1312;
																bool flag1314 = flag1313;
																if (flag1314)
																{
																	cfg.bldHpNumEnemy = flag1219;
																}
																else
																{
																	bool flag1315 = k == "buildingregennumberown";
																	bool flag1316 = flag1315;
																	bool flag1317 = flag1316;
																	bool flag1318 = flag1317;
																	bool flag1319 = flag1318;
																	bool flag1320 = flag1319;
																	bool flag1321 = flag1320;
																	bool flag1322 = flag1321;
																	if (flag1322)
																	{
																		cfg.bldRegenOwn = flag1219;
																	}
																	else
																	{
																		bool flag1323 = k == "buildingregennumberenemy";
																		bool flag1324 = flag1323;
																		bool flag1325 = flag1324;
																		bool flag1326 = flag1325;
																		bool flag1327 = flag1326;
																		bool flag1328 = flag1327;
																		bool flag1329 = flag1328;
																		bool flag1330 = flag1329;
																		if (flag1330)
																		{
																			cfg.bldRegenEnemy = flag1219;
																		}
																		else
																		{
																			bool flag1331 = k == "regennumberenemy";
																			bool flag1332 = flag1331;
																			bool flag1333 = flag1332;
																			bool flag1334 = flag1333;
																			bool flag1335 = flag1334;
																			bool flag1336 = flag1335;
																			bool flag1337 = flag1336;
																			bool flag1338 = flag1337;
																			if (flag1338)
																			{
																				cfg.regenNumEnemy = flag1219;
																			}
																			else
																			{
																				bool flag1339 = k == "bombbydamage";
																				bool flag1340 = flag1339;
																				bool flag1341 = flag1340;
																				bool flag1342 = flag1341;
																				bool flag1343 = flag1342;
																				bool flag1344 = flag1343;
																				bool flag1345 = flag1344;
																				bool flag1346 = flag1345;
																				if (flag1346)
																				{
																					cfg.bombByDamage = flag1219;
																				}
																				else
																				{
																					bool flag1347 = k == "statnumberbold";
																					bool flag1348 = flag1347;
																					bool flag1349 = flag1348;
																					bool flag1350 = flag1349;
																					bool flag1351 = flag1350;
																					bool flag1352 = flag1351;
																					bool flag1353 = flag1352;
																					bool flag1354 = flag1353;
																					if (flag1354)
																					{
																						cfg.statBold = flag1219;
																					}
																					else
																					{
																						bool flag1355 = k == "statnumberoutline";
																						bool flag1356 = flag1355;
																						bool flag1357 = flag1356;
																						bool flag1358 = flag1357;
																						bool flag1359 = flag1358;
																						bool flag1360 = flag1359;
																						bool flag1361 = flag1360;
																						bool flag1362 = flag1361;
																						if (flag1362)
																						{
																							cfg.statOutline = flag1219;
																						}
																						else
																						{
																							bool flag1363 = k == "hpnumberbypalette";
																							bool flag1364 = flag1363;
																							bool flag1365 = flag1364;
																							bool flag1366 = flag1365;
																							bool flag1367 = flag1366;
																							bool flag1368 = flag1367;
																							bool flag1369 = flag1368;
																							bool flag1370 = flag1369;
																							if (flag1370)
																							{
																								cfg.hpNumByPalette = flag1219;
																							}
																							else
																							{
																								bool flag1371 = k == "staminanumberbypalette";
																								bool flag1372 = flag1371;
																								bool flag1373 = flag1372;
																								bool flag1374 = flag1373;
																								bool flag1375 = flag1374;
																								bool flag1376 = flag1375;
																								bool flag1377 = flag1376;
																								bool flag1378 = flag1377;
																								if (flag1378)
																								{
																									cfg.stamNumByPalette = flag1219;
																								}
																								else
																								{
																									bool flag1379 = k == "pathown";
																									bool flag1380 = flag1379;
																									bool flag1381 = flag1380;
																									bool flag1382 = flag1381;
																									bool flag1383 = flag1382;
																									bool flag1384 = flag1383;
																									bool flag1385 = flag1384;
																									bool flag1386 = flag1385;
																									if (flag1386)
																									{
																										cfg.pathOwn = flag1219;
																									}
																									else
																									{
																										bool flag1387 = k == "pathtrail";
																										bool flag1388 = flag1387;
																										bool flag1389 = flag1388;
																										bool flag1390 = flag1389;
																										bool flag1391 = flag1390;
																										bool flag1392 = flag1391;
																										bool flag1393 = flag1392;
																										bool flag1394 = flag1393;
																										if (flag1394)
																										{
																											cfg.pathTrail = flag1219;
																										}
																										else
																										{
																											bool flag1395 = k == "pathpredict";
																											bool flag1396 = flag1395;
																											bool flag1397 = flag1396;
																											bool flag1398 = flag1397;
																											bool flag1399 = flag1398;
																											bool flag1400 = flag1399;
																											bool flag1401 = flag1400;
																											bool flag1402 = flag1401;
																											if (flag1402)
																											{
																												cfg.pathPredict = flag1219;
																											}
																											else
																											{
																												bool flag1403 = k == "bombmap";
																												bool flag1404 = flag1403;
																												bool flag1405 = flag1404;
																												bool flag1406 = flag1405;
																												bool flag1407 = flag1406;
																												bool flag1408 = flag1407;
																												bool flag1409 = flag1408;
																												bool flag1410 = flag1409;
																												if (flag1410)
																												{
																													cfg.bombMap = flag1219;
																												}
																												else
																												{
																													bool flag1411 = k == "bombmap3d";
																													bool flag1412 = flag1411;
																													bool flag1413 = flag1412;
																													bool flag1414 = flag1413;
																													bool flag1415 = flag1414;
																													bool flag1416 = flag1415;
																													bool flag1417 = flag1416;
																													bool flag1418 = flag1417;
																													if (flag1418)
																													{
																														cfg.bombMap3D = flag1219;
																													}
																													else
																													{
																														bool flag1419 = k == "bombmapnumbers";
																														bool flag1420 = flag1419;
																														bool flag1421 = flag1420;
																														bool flag1422 = flag1421;
																														bool flag1423 = flag1422;
																														bool flag1424 = flag1423;
																														bool flag1425 = flag1424;
																														bool flag1426 = flag1425;
																														if (flag1426)
																														{
																															cfg.bombMapNumbers = flag1219;
																														}
																														else
																														{
																															bool flag1427 = k == "bombmapallnumbers";
																															bool flag1428 = flag1427;
																															bool flag1429 = flag1428;
																															bool flag1430 = flag1429;
																															bool flag1431 = flag1430;
																															bool flag1432 = flag1431;
																															bool flag1433 = flag1432;
																															bool flag1434 = flag1433;
																															if (flag1434)
																															{
																																cfg.bombMapAllNumbers = flag1219;
																															}
																															else
																															{
																																bool flag1435 = k == "pathselectedonly";
																																bool flag1436 = flag1435;
																																bool flag1437 = flag1436;
																																bool flag1438 = flag1437;
																																bool flag1439 = flag1438;
																																bool flag1440 = flag1439;
																																bool flag1441 = flag1440;
																																bool flag1442 = flag1441;
																																if (flag1442)
																																{
																																	cfg.pathSelectedOnly = flag1219;
																																}
																																else
																																{
																																	bool flag1443 = k == "pathontop";
																																	bool flag1444 = flag1443;
																																	bool flag1445 = flag1444;
																																	bool flag1446 = flag1445;
																																	bool flag1447 = flag1446;
																																	bool flag1448 = flag1447;
																																	bool flag1449 = flag1448;
																																	bool flag1450 = flag1449;
																																	if (flag1450)
																																	{
																																		cfg.pathOnTop = flag1219;
																																	}
																																	else
																																	{
																																		bool flag1451 = k == "pathmarker";
																																		bool flag1452 = flag1451;
																																		bool flag1453 = flag1452;
																																		bool flag1454 = flag1453;
																																		bool flag1455 = flag1454;
																																		bool flag1456 = flag1455;
																																		bool flag1457 = flag1456;
																																		bool flag1458 = flag1457;
																																		if (flag1458)
																																		{
																																			cfg.pathMarker = flag1219;
																																		}
																																	}
																																}
																															}
																														}
																													}
																												}
																											}
																										}
																									}
																								}
																							}
																						}
																					}
																				}
																			}
																		}
																	}
																}
															}
														}
													}
												}
											}
										}
									}
								}
							}
						}
						return true;
						IL_53B2:
						bool flag1459 = !ResourcePackManager.TryParseHpBool(val, out flag1219);
						bool flag1460 = flag1459;
						bool flag1461 = flag1460;
						bool flag1462 = flag1461;
						bool flag1463 = flag1462;
						bool flag1464 = flag1463;
						bool flag1465 = flag1464;
						bool flag1466 = flag1465;
						if (flag1466)
						{
							problems.Add(string.Concat(new object[]
							{
								"line ",
								lineIdx + 1,
								": ",
								key,
								" must be true/false"
							}));
							return true;
						}
						bool flag1467 = k == "staminacolor";
						bool flag1468 = flag1467;
						bool flag1469 = flag1468;
						bool flag1470 = flag1469;
						bool flag1471 = flag1470;
						bool flag1472 = flag1471;
						bool flag1473 = flag1472;
						bool flag1474 = flag1473;
						if (flag1474)
						{
							cfg.staminaColor = flag1219;
						}
						else
						{
							bool flag1475 = k == "ammocolor";
							bool flag1476 = flag1475;
							bool flag1477 = flag1476;
							bool flag1478 = flag1477;
							bool flag1479 = flag1478;
							bool flag1480 = flag1479;
							bool flag1481 = flag1480;
							bool flag1482 = flag1481;
							if (flag1482)
							{
								cfg.ammoColor = flag1219;
							}
							else
							{
								bool flag1483 = k == "staminaknightonly";
								bool flag1484 = flag1483;
								bool flag1485 = flag1484;
								bool flag1486 = flag1485;
								bool flag1487 = flag1486;
								bool flag1488 = flag1487;
								bool flag1489 = flag1488;
								bool flag1490 = flag1489;
								if (flag1490)
								{
									cfg.knightStaminaOnly = flag1219;
								}
								else
								{
									bool flag1491 = k == "ammoring";
									bool flag1492 = flag1491;
									bool flag1493 = flag1492;
									bool flag1494 = flag1493;
									bool flag1495 = flag1494;
									bool flag1496 = flag1495;
									bool flag1497 = flag1496;
									bool flag1498 = flag1497;
									if (flag1498)
									{
										cfg.ammoRing = flag1219;
									}
									else
									{
										bool flag1499 = k == "ammonumber";
										bool flag1500 = flag1499;
										bool flag1501 = flag1500;
										bool flag1502 = flag1501;
										bool flag1503 = flag1502;
										bool flag1504 = flag1503;
										bool flag1505 = flag1504;
										bool flag1506 = flag1505;
										if (flag1506)
										{
											cfg.ammoNumber = flag1219;
										}
										else
										{
											bool flag1507 = k == "ammonumbercolorbyammo";
											bool flag1508 = flag1507;
											bool flag1509 = flag1508;
											bool flag1510 = flag1509;
											bool flag1511 = flag1510;
											bool flag1512 = flag1511;
											bool flag1513 = flag1512;
											bool flag1514 = flag1513;
											if (flag1514)
											{
												cfg.numByAmmo = flag1219;
											}
											else
											{
												bool flag1515 = k == "ammonumberoutline";
												bool flag1516 = flag1515;
												bool flag1517 = flag1516;
												bool flag1518 = flag1517;
												bool flag1519 = flag1518;
												bool flag1520 = flag1519;
												bool flag1521 = flag1520;
												bool flag1522 = flag1521;
												if (flag1522)
												{
													cfg.numOutline = flag1219;
												}
												else
												{
													bool flag1523 = k == "ammonumberbold";
													bool flag1524 = flag1523;
													bool flag1525 = flag1524;
													bool flag1526 = flag1525;
													bool flag1527 = flag1526;
													bool flag1528 = flag1527;
													bool flag1529 = flag1528;
													bool flag1530 = flag1529;
													if (flag1530)
													{
														cfg.numBold = flag1219;
													}
													else
													{
														cfg.selShiftOn = flag1219;
													}
												}
											}
										}
									}
								}
							}
						}
						return true;
						IL_57EE:
						result = false;
					}
				}
			}
		}
		return result;
	}
}
}

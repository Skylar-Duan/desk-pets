# 糯米桌面动作补充提示词

2026年10月14日，使用内置 imagegen，参考 `../original/base.png`。生成图已保存到当前目录；动物造型沿用原有奶白、灰棕、蓝眼的现代布偶猫。源图均为透明PNG，不修改原始v2图集。

## 睡眠动作

Production sprite animation strip for an existing desktop pet Nuomi. Preserve exactly the referenced cream white fluffy modern 3D-toy ragdoll kitten, symmetrical taupe ears and tail, white V face, pink nose, blue-eye construction. Create ONE horizontal strip containing exactly FOUR clearly separated complete poses of this SAME cat SLEEPING on its side in a compact curled lie-down, eyes fully closed, paws tucked naturally: subtle breathing loop, no travel, same camera, same scale and baseline across poses. Anatomical total exactly two forelimbs and two hindlimbs, naturally occluded; never add extra limbs. Tail attached, complete ears and tail, generous transparent gaps and padding so each pose can be extracted intact. Transparent background and no floor, shadows, lettering, frame borders, accessories, effects or symbols. Final images will be displayed at 100-120 pixels high, so preserve clear soft sculpted-fur silhouette. Horizontal wide layout, four complete sleeping cats, no sitting poses.

## 提起与下落

Production sprite pose strip for the existing desktop pet Nuomi. Preserve exactly the referenced cream white fluffy modern 3D-toy ragdoll kitten with taupe ears and tail, white V face, blue eyes and pink nose. ONE horizontal strip with exactly FOUR separated full-body poses, same frontal camera, same overall size and centered registration. Poses 1 and 2: calmly suspended in the air as if gently picked up, two forepaws hanging and two hindpaws hanging naturally, attached tail gently changing curl. Poses 3 and 4: falling through the air, two forepaws and two hindpaws slightly braced for landing, still stationary in this strip (the application supplies the vertical movement). Four anatomical limbs only, no hands or humans, no extra planted paws, no lifted-paw-overlaid-on-four-paw anatomy. Full complete ears, paws and attached tail, generous gaps and padding. Transparent background; no floor, no shadows, no blur, no speed marks, no lettering, no borders, no props. Four complete matching cats. Keep outline, face, sculpted fluff and size consistent across all four. Do not simulate motion through vertical displacement or resizing.

## 配准与复用

新姿态由保存的原插件连通组件辅助函数提取主轮廓，再共用一个比例落入96×128格，避免每帧单独填满导致变大变小。抠除透明噪声和轮廓外碎片，不重画身体。左右步态、眨眼、坐姿及挥爪沿用已验收v2图集。原有工作语义的 `running`、`waiting`、`review` 不冒充生活动作。

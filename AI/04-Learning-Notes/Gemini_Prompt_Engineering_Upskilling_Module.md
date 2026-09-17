# Gemini API + Prompt-Engineering Upskilling Module

## الهدف من هذه الوحدة

قبل ما نبلش نبني الـ prompt template والاستدعاء الفعلي لـ Gemini حسب `TRIPLY_AI_JSON_SCHEMA_CONTRACT_v2.md`، لازم يكون عند الثلاثتنا نفس الفهم لـ:

1. كيف نستخدم Gemini REST API (auth, endpoints, request/response).
2. كيف نصمم prompt فعّال وموثوق.
3. كيف يشتغل الـ Structured/JSON-mode output، وكيف نطبّقه بالضبط على `triply-trip-plan-generation.schema.json`.

كل الأمثلة هون مبنية على بيانات Triply الحقيقية (وجهة **عمّان**) عشان نتمرن على نفس الشكل يلي رح نستخدمه بالمشروع.

---

## 1. أساسيات Gemini REST API

### 1.1 نقطتين API موجودتين حالياً

Google عندها الآن مسارين للاستدعاء:

|           | `generateContent` (الكلاسيكي)                              | `Interactions API` (الأحدث)                             |
| --------- | ---------------------------------------------------------- | ------------------------------------------------------- |
| الحالة    | لسا مدعوم بالكامل، لكن معتبر "legacy"                      | أصبح GA من يونيو 2026، وهو الموصى فيه للمشاريع الجديدة  |
| الأنسب لـ | طلبات single-shot، بدون حالة محفوظة، استقرار عالي بالإنتاج | محادثات متعددة الأدوار، agents، حالة محفوظة عند السيرفر |
| Endpoint  | `POST /v1beta/models/{model}:generateContent`              | `POST /v1beta/interactions`                             |

**بالنسبة لـ Triply تحديداً:** توليد خطة الرحلة هو طلب واحد كامل (كل بيانات الرحلة بالسياق، ورد JSON واحد كامل) — مافي حاجة لحالة محفوظة أو محادثة متعددة الأدوار بهاي المرحلة. لهيك **`generateContent` هو الخيار الأنسب والأكثر استقراراً لـ MVP**، خصوصاً إنه رسمياً موصى فيه لـ "single-shot generation" و"stability guarantees". ممكن نراجع الموضوع لاحقاً إذا صار عندنا feature زي "تعديل الخطة بالمحادثة" (FR-TRIP-005، موجودة كـ post-MVP بالعقد).

### 1.2 الـ Authentication

- بتحتاجي `GEMINI_API_KEY` (من Google AI Studio أو مشروع GCP).
- بترسليه كـ header: `x-goog-api-key: $GEMINI_API_KEY`
- **لا تحطي المفتاح بالكود مباشرة** — استخدمي environment variable، ونفس المبدأ يلي متبعينه بالمشروع (لا secrets بالـ repo).

### 1.3 شكل الطلب الأساسي (curl)

```bash
curl -X POST "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-pro:generateContent" \
  -H "x-goog-api-key: $GEMINI_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "system_instruction": {
      "parts": [{ "text": "أنت مساعد تخطيط رحلات..." }]
    },
    "contents": [
      { "role": "user", "parts": [{ "text": "خططلي رحلة 3 أيام لعمّان بميزانية 400 دينار" }] }
    ],
    "generationConfig": {
      "responseMimeType": "application/json",
      "responseSchema": { ... }
    }
  }'
```

### 1.4 أجزاء الرد المهمة

```json
{
  "candidates": [
    {
      "content": { "parts": [{ "text": "{ ... JSON string ... }" }] },
      "finishReason": "STOP"
    }
  ],
  "usageMetadata": { "promptTokenCount": 512, "candidatesTokenCount": 890 }
}
```

- الـ JSON الفعلي بيجي كـ **نص (string)** جوا `candidates[0].content.parts[0].text` — لازم تعملي `JSON.parse()` عليه أنتي.
- تأكدي دايماً من `finishReason == "STOP"`. إذا رجع `MAX_TOKENS` معناها الرد انقطع ولازم تزيدي `maxOutputTokens` أو تقصّري السياق.
- راقبي `usageMetadata` للتكلفة — مهم لأن قائمة الأماكن (Place list) اللي رح ترسليها بالسياق ممكن تكبر بسرعة.

---

## 2. تصميم الـ Prompts (Prompt Design)

### 2.1 المبادئ الأساسية

1. **كوني محددة، مو عامة.** بدل "اقترح أماكن بعمّان"، حددي بالضبط شو المطلوب (المدة، الميزانية، الاهتمامات، القيود).
2. **افصلي التعليمات الثابتة عن بيانات الطلب.** التعليمات الثابتة (القواعد، القيود، شكل الإخراج) تروح بـ `system_instruction`. بيانات الرحلة المحددة (تواريخ، ميزانية، وجهة) تروح بـ `contents` (user message).
3. **زوّديه بالبيانات كنص واضح، مو كوصف عام.** لازم يشوف Gemini قائمة الأماكن الفعلية (بالاسم بالضبط) عشان يقدر يختار منها — مش يخمّن أسماء من عنده.
4. **كرري القاعدة الحرجة أكثر من مرة إذا لزم.** الموديلات أحياناً بتتجاهل تفصيل واحد إذا انذكر مرة وحدة بس داخل schema description طويل.
5. **جربي وعدّلي (iterate).** ما في prompt مثالي من أول مرة — لازم تشغّلي نفس الطلب كم مرة وتشوفي هل الرد ثابت ومتوافق مع القواعد.

### 2.2 مثال System Instruction مبني على عقد Triply

هاد مثال مختصر مبني فعلياً على القواعد يلي بالـ Contract (§2، §5):

```
أنت مولّد خطط رحلات لتطبيق Triply. اتّبعي هذه القواعد بدقة:

1. استخدمي فقط الوجهات والأماكن المذكورة بالضبط بالقائمة المرفقة أدناه.
   لا تخترعي أي اسم مكان أو وجهة غير موجود بالقائمة (0% اختراع أماكن).
2. لكل destination_option: اختاري مكان إقامة واحد فقط (فئة ACCOMMODATION)
   وحطيه في accommodation، وليس ضمن days.
3. كل يوم (day) لازم يحتوي مطعم واحد على الأقل (فئة RESTAURANT).
4. عبر كل أيام الخطة، لازم يكون في عنصر واحد على الأقل من فئة TRANSPORT
   (مثال: نقل من/إلى المطار).
5. لا ترجعي أي أسعار أو أرقام تكلفة — التسعير محسوب من عندنا بشكل منفصل.
6. لا ترجعي أي معرّف قاعدة بيانات (id) — فقط الأسماء النصية بالضبط كما وردت.
```

### 2.3 مثال User Content (بيانات الطلب + السياق)

```
بيانات الرحلة:
- planning_mode: DESTINATION_FIRST
- الوجهة: Amman
- المدة: 3 أيام (2026-11-10 إلى 2026-11-12)
- عدد الأشخاص: 2
- الميزانية الكلية: 400 JOD
- الاهتمامات: History, Food
- travel_style: Cultural

الأماكن المتاحة في Amman (استخدمي الاسم بالضبط كما هو):

[ACCOMMODATION]
- Jordan Tower Hotel (Low budget tier)
- AlQasr Metropole Hotel (Medium budget tier)
- Four Seasons Hotel Amman (High budget tier)

[RESTAURANT]
- Hashem Restaurant (Low)
- Jordan Heritage Restaurant (Medium)
- Levant Restaurant (Medium/High)

[ATTRACTION]
- Amman Citadel & Jordan Archaeological Museum
- Roman Theatre & Museum of Popular Traditions
- Jerash Archaeological Site & Museum

[TRANSPORT]
- Airport Transfer – QAIA to Amman
- Intra-city Taxi (short ride)
- Jordan Pass
```

**ملاحظة مهمة:** هاد السياق لازم يتبنى ديناميكياً من `Place.csv` (فلترة `is_active = True` و`destination_id` المطلوب) و`Extra_AI_Context.csv` (لتوجيه اختيار الـ budget_tier والاهتمامات) — مش يكتب يدوياً. هاد هو دور الـ "Prompt Context Builder" يلي حكينا عنه.

---

## 3. Structured / JSON-Mode Output

### 3.1 الفرق بين `responseSchema` و `responseJsonSchema`

Gemini عنده طريقتين لتقييد شكل الرد، وهاي نقطة **مهمة جداً** لعقد Triply لأن الـ schema تبعنا يستخدم `$defs` و`$ref`:

|                        | `responseSchema` (القديم)                          | `responseJsonSchema` (الأحدث)                                                                                                                                                                     |
| ---------------------- | -------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| المعيار                | Subset من OpenAPI 3.0                              | JSON Schema فعلي (draft)                                                                                                                                                                          |
| يدعم `$defs` / `$ref`  | لا (لازم "تفلّطحي" الـ schema يدوياً)                | **نعم**                                                                                                                                                                                           |
| يدعم `anyOf` / `oneOf` | محدود                                              | نعم                                                                                                                                                                                               |
| القيود المدعومة        | type, format, enum, items, properties, required... | $id, $defs, $ref, $anchor, type, format, enum, items, prefixItems, minItems, maxItems, minimum, maximum, anyOf/oneOf, properties, additionalProperties, required + `propertyOrdering` (غير قياسي) |

بما إن ملف `triply-trip-plan-generation.schema.json` عندنا مبني أصلاً بـ `$defs`/`$ref` (draft 2020-12)، **الخيار الأنسب هو `responseJsonSchema`** — بترسلي نفس الملف تقريباً بدون تعديل كبير، بدل ما تعيدي كتابته بصيغة OpenAPI المسطّحة.

### 3.2 مثال استدعاء (Python)

```python
from google import genai
import json

client = genai.Client(api_key="...")

with open("triply-trip-plan-generation.schema.json") as f:
    schema = json.load(f)

# تعديل maxItems حسب planning_mode (Contract §5, خطوة 0)
planning_mode = "DESTINATION_FIRST"
schema["properties"]["destination_options"]["maxItems"] = (
    1 if planning_mode == "DESTINATION_FIRST" else 3
)

response = client.models.generate_content(
    model="gemini-2.5-pro",
    contents=user_prompt,  # النص من §2.3
    config={
        "system_instruction": system_instruction,  # النص من §2.2
        "response_mime_type": "application/json",
        "response_json_schema": schema,
    },
)

trip_plan = json.loads(response.text)
```

### 3.3 نقاط لازم تنتبهوا لها

- **الـ schema المرجوع مضمون بالشكل، مش بالمحتوى.** يعني Gemini رح يرجع JSON مطابق للبنية (الحقول والأنواع صح)، لكن **مش مضمون** إن كل `place_name` فعلاً موجود بالداتا — هاد هو بالضبط سبب وجود خطوة "Dataset Grounding" (§5 بالعقد) اللي لازم تتعمل بعد الرد، مو بدلاً عنه.
- **`propertyOrdering`** مفيدة إذا لاحظتوا إن ترتيب الحقول أثّر على جودة الرد، بس مش أولوية بالبداية.
- **Cyclic references محدودة الدعم** — الـ schema تبعنا مالوش دورات (cycles) حقيقية فهاي مو مشكلة عنا.
- إذا الـ schema رجع فاضي أو `finishReason: MAX_TOKENS`، جربي تصغّري قائمة الأماكن المرسلة بالسياق (مثلاً فلترة حسب budget_tier قبل الإرسال) بدل ما تكبّري الـ token limit فقط.

---

## 4. تمرين عملي (مقترح للفريق)

1. جهزوا سكريبت بسيط (Python) ياخد: وجهة (Amman)، مدة (3 أيام)، ميزانية (400 JOD)، اهتمامات (History, Food).
2. ابنوا منه الـ context (قائمة أماكن Amman الفعلية من `Place.csv` أعلاه).
3. استدعوا Gemini بـ `responseJsonSchema` = محتوى `triply-trip-plan-generation.schema.json`.
4. تأكدوا يدوياً إن الرد:
   - كل `place_name` من القائمة المرسلة بالضبط (مافي اسم مخترع).
   - `accommodation.place_name` من فئة ACCOMMODATION فقط.
   - كل يوم فيه مطعم واحد عالأقل.
   - فيه عنصر transport واحد عالأقل بكل الخطة.
   - `days.length` = 3 بالضبط.
5. جربوا نفس الطلب 3 مرات وقارنوا الثبات (consistency) بين الردود.

---

## 5. أخطاء شائعة

| المشكلة                                   | السبب المحتمل                                         | الحل                                                                                              |
| ----------------------------------------- | ----------------------------------------------------- | ------------------------------------------------------------------------------------------------- |
| رد فيه اسم مكان مش موجود بالداتا          | الموديل "خمّن" اسم مشابه أو دمج اسمين                  | أكدي القاعدة بالـ system instruction + طبّقي grounding validation إلزامياً                          |
| رد ناقص أو JSON غير مكتمل                 | تجاوز `maxOutputTokens`                               | قصّري قائمة الأماكن بالسياق، أو زيدي الحد                                                          |
| الموديل رجّع سعر أو تكلفة رغم منعه         | التعليمة مو واضحة بما فيه الكفاية                     | اذكري صراحة بالـ schema description **و** بالـ system instruction إنه ممنوع يرجع أي رقم تكلفة     |
| ترتيب الحقول غير متوقع                    | Gemini ما يحترم الترتيب دايماً بدون `propertyOrdering` | أضيفي `propertyOrdering` إذا الترتيب مهم لمعالجتك                                                 |
| فشل متكرر في التحقق (`FAILED_VALIDATION`) | الـ prompt عام جداً أو القائمة المرسلة ناقصة           | راجعي هل القائمة المرسلة تغطي كل الفئات المطلوبة (ACCOMMODATION, RESTAURANT, TRANSPORT على الأقل) |

---

## 6. مصادر إضافية

- [Gemini API — Structured Output](https://ai.google.dev/gemini-api/docs) (JSON mode, responseSchema/responseJsonSchema)
- [Gemini API Reference](https://ai.google.dev/api) (generateContent, Interactions API)
- ملفات المشروع الداخلية: `TRIPLY_AI_JSON_SCHEMA_CONTRACT_v2.md`, `triply-trip-plan-generation.schema.json`, `Extra_AI_Context.csv`

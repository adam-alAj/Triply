Please review and update the following security items:

1. Authentication & Authorization
 Needs to be fixed
•	No refresh tokens, no logout/revocation. Only one access token is issued at login. If it's ever stolen, there's no way to cancel it before it expires. Needs a team decision: add refresh tokens, or at least shorten the token lifetime.
•	No email verification.
•	No password reset flow.

________________________________________
2. Input Validation & Sanitization
Needs to be fixed
•	No limit on how many interests a user can send. The InterestCategoryIds list has no maximum size — a user could send a huge list in one request. 
•	No limit on request size. There is nothing in the code stopping someone from sending an unusually large request body.

________________________________________
3. Rate Limiting & Abuse Protection
• The rate limit is shared by everyone, not per user
Right now, the login limit (5 tries/minute) and the general API limit (10 requests/minute) apply to all users combined, not to each person separately. That means one person spamming login attempts can lock everyone else out of logging in for a minute. This is confirmed by the project's own test file.
Fix: limit each user/IP separately instead of sharing one global counter. Already written and ready to apply.

• AI generation has no dedicated limit
Generating a trip with AI is the most expensive action in the app (it costs money and takes time), but it currently shares the same small limit as regular actions like viewing a trip. This means it can be spammed and drive up costs.
Fix: give AI generation its own limit — 10 generations per hour, per user. Already written and ready to apply.

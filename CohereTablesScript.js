use Cohere

// Users collection
db.createCollection("Accounts", {
    validator: {
        $jsonSchema: {
            bsonType: "object",
            required: [
                "Email",
                "EncryptedPassword",
                "EncryptionSalt",
                "Roles",
                "OnboardingStatus",
                "HasAgreededToTerms",
                "IsEmailConfirmed",
                "IsPhoneConfirmed",
                "IsAccountLocked",
                "IsPushNotificationsEnabled",
                "IsEmailNotificationsEnabled"
            ],
            properties: {
                Email: {
                    bsonType: "string",
                    description: "must be a string and required"
                },
                EncryptedPassword: {
                    bsonType: "string",
                    description: "must be a string and required"
                },
                EncryptionSalt: {
                    bsonType: "string",
                    description: "must be a string and required"
                },
                Roles: {
                    bsonType: "array",
                    description: "must be an array"
                },
                VerificationToken: {
                    bsonType: "string",
                    description: "must be a string"
                },
                VerificationTokenExpiration: {
                    bsonType: "date",
                    description: "must be a string"
                },
                OAuthToken: {
                    bsonType: "string",
                    description: "must be a string"
                },
                OnboardingStatus: {
                    bsonType: "string",
                    description: "must be a string and required"
                },
                SecurityAnswers: {
                    bsonType: "object",
                    required: ["QuestionId", "Answer"],
                    properties: {
                        QuestionId: {
                            bsonType: "string",
                            description: "must be a string"
                        },
                        Answer: {
                            bsonType: "string",
                            description: "must be a string"
                        }
                    }
                },
                NumLogonAttempts: {
                    bsonType: "int",
                    description: "must be an integer"
                },
                IsEmailConfirmed: {
                    bsonType: "bool",
                    description: "must be a boolean and required"
                },
                IsPhoneConfirmed: {
                    bsonType: "bool",
                    description: "must be a boolean and required"
                },
                IsAccountLocked: {
                    bsonType: "bool",
                    description: "must be a boolean and required"
                },
                IsPushNotificationsEnabled: {
                    bsonType: "bool",
                    description: "must be a boolean and required"
                },
                IsEmailNotificationsEnabled: {
                    bsonType: "bool",
                    description: "must be a boolean and required"
                },
                CreateTime: {
                    bsonType: "date",
                    description: "must be a date and required"
                },
                UpdateTime: {
                    bsonType: "date",
                    description: "must be a date and required"
                }
            }
        }
    },
    validationAction: "warn"
})


// Clients collection
db.createCollection("Users", {
    validator: {
        $jsonSchema: {
            bsonType: "object",
            required: ["AccountId", "FirstName", "LastName", "IsCohealer"],
            properties: {
                AccountId: {
                    bsonType: "string",
                    description: "must be a string"
                },
                Title: {
                    bsonType: "string",
                    description: "must be a string"
                },
                FirstName: {
                    bsonType: "string",
                    description: "must be a string and required"
                },
                MiddleName: {
                    bsonType: "string",
                    description: "must be a string"
                },
                LastName: {
                    bsonType: "string",
                    description: "must be a string and required"
                },
                NameSuffix: {
                    bsonType: "string",
                    description: "must be a string"
                },
                HasAgreedToTerms: {
                    bsonType: "bool",
                    description: "must be a boolean and required"
                },
                IsCohealer: {
                    bsonType: "bool",
                    description: "must be a bool and required"
                },
                ClientPreferences: {
                    bsonType: "object",
                    properties: {
                        Interests: {
                            bsonType: "object",
                            description: "must be an object",
                        },
                        Experiences: {
                            bsonType: "object",
                            description: "must be an object"
                        },
                        Curiosities: {
                            bsonType: "object",
                            description: "must be an object"
                        }
                    }
                },
                BirthDate: {
                    bsonType: "date",
                    description: "must be a date"
                },
                SocialSecurityNumber: {
                    bsonType: "string",
                    description: "must be a string"
                },
                StreetAddress: {
                    bsonType: "string",
                    description: "must be a string"
                },
                Apt: {
                    bsonType: "string",
                    description: "must be a string"
                },
                City: {
                    bsonType: "string",
                    description: "must be a string"
                },
                StateCode: {
                    bsonType: "string",
                    description: "must be a string"
                },
                Zip: {
                    bsonType: "string",
                    description: "must be a string"
                },
                CountryCode: {
                    bsonType: "string",
                    description: "must be a string"
                },
                Bio: {
                    bsonType: "string",
                    description: "must be a string"
                },
                TimeZoneId: {
                    bsonType: "string",
                    description: "must be a string"
                },
                LanguageCodes: {
                    bsonType: "array",
                    description: "must be an array"
                },
                Location: {
                    bsonType: "object",
                    required: ["Latitude", "Longitude"],
                    properties: {
                        Latitude: {
                            bsonType: "string",
                            description: "must be a string"
                        },
                        Longitude: {
                            bsonType: "string",
                            description: "must be a string"
                        }
                    }
                },
                Phone1: {
                    bsonType: "object",
                    required: ["PhoneNumber"],
                    properties: {
                        PhoneNumber: {
                            bsonType: "string",
                            description: "must be a string"
                        },
                        PhoneType: {
                            bsonType: "string",
                            description: "must be a string"
                        }
                    }
                },
                Phone2: {
                    bsonType: "object",
                    required: ["PhoneNumber"],
                    properties: {
                        PhoneNumber: {
                            bsonType: "string",
                            description: "must be a string"
                        },
                        PhoneType: {
                            bsonType: "string",
                            description: "must be a string"
                        }
                    }
                },
                PlaidId: {
                    bsonType: "string",
                    description: "must be a string"
                },
                StripeId: {
                    bsonType: "string",
                    description: "must be a string"
                },
                // Cohealer fields
                BusinessName: {
                    bsonType: "string",
                    description: "must be a string"
                },
                BusinessType: {
                    bsonType: "string",
                    description: "must be a string and required"
                },
                CustomBusinessType: {
                    bsonType: "string",
                    description: "must be a string"
                },
                SocialMediaLinks: {
                    bsonType: "object",
                    description: "must be an object"
                },
                Certification: {
                    bsonType: "string",
                    description: "must be a string"
                },
                Occupation: {
                    bsonType: "string",
                    description: "must be a string and required"
                },
                CustomerLabelPreference: {
                    bsonType: "string",
                    description: "must be a string"
                },
                CreateTime: {
                    bsonType: "date",
                    description: "must be a date and required"
                },
                UpdateTime: {
                    bsonType: "date",
                    description: "must be a date and required"
                },
                CreateTime: {
                    bsonType: "date",
                    description: "must be a date and required"
                },
                UpdateTime: {
                    bsonType: "date",
                    description: "must be a date and required"
                }
            }
        }
    },
    validationAction: "warn"
})

//SecurityQuestions collection
db.createCollection("SecurityQuestions", {
    validator: {
        $jsonSchema: {
            bsonType: "object",
            required: ["Text"],
            properties: {
                _id: {
                    bsonType: "string",
                    description: "must be a string and autogenerated"
                },
                Text: {
                    bsonType: "string",
                    description: "must be a string and required"
                },
                CreateTime: {
                    bsonType: "date",
                    description: "must be a date and required"
                },
                UpdateTime: {
                    bsonType: "date",
                    description: "must be a date and required"
                }
            }
        }
    },
    validationAction: "error"
})

db.SecurityQuestions.insertMany([
    {_id: new ObjectId().str, Text: "What is the name of your favorite childhood friend?", CreateTime: new Date(), UpdateTime: new Date() },
    {_id: new ObjectId().str, Text: "What street did you live on in third grade?", CreateTime: new Date(), UpdateTime: new Date() }, 
    {_id: new ObjectId().str, Text: "What is the middle name of your youngest child?", CreateTime: new Date(), UpdateTime: new Date() },
    {_id: new ObjectId().str, Text: "What school did you attend for sixth grade?", CreateTime: new Date(), UpdateTime: new Date() },
    {_id: new ObjectId().str, Text: "In what city or town did your mother and father meet?", CreateTime: new Date(), UpdateTime: new Date() },
    {_id: new ObjectId().str, Text: "Where were you when you had your first kiss?", CreateTime: new Date(), UpdateTime: new Date() },
    {_id: new ObjectId().str, Text: "What is the first name of the boy or girl that you first kissed?", CreateTime: new Date(), UpdateTime: new Date() },
    {_id: new ObjectId().str, Text: "What was the last name of your third grade teacher?", CreateTime: new Date(), UpdateTime: new Date() },
    {_id: new ObjectId().str, Text: "What is your youngest brother’s birthday month and year? (e.g., January 1900)", CreateTime: new Date(), UpdateTime: new Date() },
    {_id: new ObjectId().str, Text: "What high school did you attend?", CreateTime: new Date(), UpdateTime: new Date() },
    {_id: new ObjectId().str, Text: "What is your mother’s middle name?", CreateTime: new Date(), UpdateTime: new Date() },
    {_id: new ObjectId().str, Text: "What was the name of your first pet?", CreateTime: new Date(), UpdateTime: new Date() },
    {_id: new ObjectId().str, Text: "What is the first name of your best friend?", CreateTime: new Date(), UpdateTime: new Date() }
])

//Preferences collection
db.createCollection("Preferences", {
    validator: {
        $jsonSchema: {
            bsonType: "object",
            required: ["Name"],
            properties: {
                _id: {
                    bsonType: "string",
                    description: "must be a string and autogenerated"
                },
                Name: {
                    bsonType: "string",
                    description: "must be a string and required"
                },
                CreateTime: {
                    bsonType: "date",
                    description: "must be a date and required"
                },
                UpdateTime: {
                    bsonType: "date",
                    description: "must be a date and required"
                }
            }
        }
    },
    validationAction: "error"
})

db.Preferences.insertMany([
    {_id: new ObjectId().str, Name: "Reiki", CreateTime: new Date(), UpdateTime: new Date() },
    {_id: new ObjectId().str, Name: "Meditation", CreateTime: new Date(), UpdateTime: new Date() },
    {_id: new ObjectId().str, Name: "Life Coaching", CreateTime: new Date(), UpdateTime: new Date() },
    {_id: new ObjectId().str, Name: "Self Development", CreateTime: new Date(), UpdateTime: new Date() },
    {_id: new ObjectId().str, Name: "Parenting", CreateTime: new Date(), UpdateTime: new Date() },
    {_id: new ObjectId().str, Name: "Relationships", CreateTime: new Date(), UpdateTime: new Date() },
    {_id: new ObjectId().str, Name: "Shamanism", CreateTime: new Date(), UpdateTime: new Date() },
    {_id: new ObjectId().str, Name: "Intuitive Healing", CreateTime: new Date(), UpdateTime: new Date() },
    {_id: new ObjectId().str, Name: "Professional Coaching", CreateTime: new Date(), UpdateTime: new Date() },
    {_id: new ObjectId().str, Name: "Wellness Coaching", CreateTime: new Date(), UpdateTime: new Date() },
    {_id: new ObjectId().str, Name: "Business Coaching", CreateTime: new Date(), UpdateTime: new Date() },
    {_id: new ObjectId().str, Name: "Emotional Wellbeing", CreateTime: new Date(), UpdateTime: new Date() }
])

// Contribution collection
db.createCollection("Contributions", {
    validator: {
        $jsonSchema: {
            bsonType: "object",
            required: [
                "UserId",
                "Title",
                "Status",
                "Categories",
                "Type",
                "Purpose",
                "WhoIAM",
                "WhatWeDo",
                "LanguageCodes",
                "PaymentInfo",
                "HasAgreedContributionTerms"
            ],
            properties: {
                UserId: {
                    bsonType: "string",
                    description: "must be a string and required"
                },
                Title: {
                    bsonType: "string",
                    description: "must be a string and required"
                },
                Status: {
                    bsonType: "string",
                    description: "must be a string and required"
                },
                Categories: {
                    bsonType: "array",
                    description: "must be an array and required"
                },
                Type: {
                    bsonType: "string",
                    description: "must be a string and required"
                },
                IsLive: {
                    bsonType: "bool",
                    description: "must be a bool"
                },
                IsAlwaysOnSale: {
                    bsonType: "bool",
                    description: "must be a bool"
                },
                Location: {
                    bsonType: "string",
                    description: "must be a string"
                },
                PreviewContentUrls: {
                    bsonType: "array",
                    description: "must be an array"
                },
                Purpose: {
                    bsonType: "string",
                    description: "must be a string and required"
                },
                WhoIAM: {
                    bsonType: "string",
                    description: "must be a string and required"
                },
                WhatWeDo: {
                    bsonType: "string",
                    description: "must be a string and required"
                },
                Preparation: {
                    bsonType: "string",
                    description: "must be a string"
                },
                LanguageCodes: {
                    bsonType: "array",
                    description: "must be an array and required"
                },
                IsSpeakingLanguageRequired: {
                    bsonType: "bool",
                    description: "must be a bool"
                },
                MinAge: {
                    bsonType: "string",
                    description: "must be a string"
                },
                Gender: {
                    bsonType: "string",
                    description: "must be a string"
                },
                Sessions: {
                    bsonType: "array",
                    description: "must be an array of onjects and required"
                },
                Enrollment:  {
                    bsonType: "object",
                    required: ["FromDate", "ToDate"],
                    properties: {
                        FromDate: {
                            bsonType: "date",
                            description: "must be a date and required"
                        },
                        ToDate: {
                            bsonType: "date",
                            description: "must be a date and required"
                        }
                    }
                },
                PaymentInfo:  {
                    bsonType: "object",
                    required: ["Cost", "PaymentOptions"],
                    properties: {
                        Cost: {
                            bsonType: "decimal",
                            description: "must be a decimal and required"
                        },
                        PaymentOptions: {
                            bsonType: "array",
                            description: "must be an array and required"
                        }
                    }
                },
                InvitationOnly: {
                    bsonType: "bool",
                    description: "must be a bool"
                },
                HasAgreedContributionTerms: {
                    bsonType: "bool",
                    description: "must be a bool"
                },
                Rating: {
                    bsonType: "double",
                    description: "must be a double"
                },
                LikesNumber: {
                    bsonType: "int",
                    description: "must be a int"
                },
                Reviews: {
                    bsonType: "array",
                    description: "must be an array and required"
                },
                TimeRange:  {
                    bsonType: "object",
                    required: ["StartTime", "EndTime"],
                    properties: {
                        StartTime: {
                            bsonType: "date",
                            description: "must be a date and required"
                        },
                        EndTime: {
                            bsonType: "date",
                            description: "must be a date and required"
                        }
                    }
                },
                CreateTime: {
                    bsonType: "date",
                    description: "must be a date and required"
                },
                UpdateTime: {
                    bsonType: "date",
                    description: "must be a date and required"
                }                
            }
        }
    },
    validationAction: "warn"
});

// Purchases collection
db.createCollection("Purchases", {
    validator: {
        $jsonSchema: {
            bsonType: "object",
            required: [
                "UserId",
                "ContributionId",
                "PaymentOption",
                "Payments"
            ],
            properties: {
                UserId: {
                    bsonType: "string",
                    description: "must be a string and required"
                },
                ContributionId: {
                    bsonType: "string",
                    description: "must be a string and required"
                },
                PaymentOption: {
                    bsonType: "string",
                    description: "must be a string and required"
                },
                SplitNumbers: {
                    bsonType: "int",
                    description: "must be an int"
                },
                SplitPeriod: {
                    bsonType: "string",
                    description: "must be a string"
                },
                Payments: {
                    bsonType: "array",
                    description: "must be an array and required",
                },
                CreateTime: {
                    bsonType: "date",
                    description: "must be a date and required"
                },
                UpdateTime: {
                    bsonType: "date",
                    description: "must be a date and required"
                }                
            }
        }
    },
    validationAction: "warn"
});

// PeerChats collection
db.createCollection("PeerChats", {
    validator: {
        $jsonSchema: {
            bsonType: "object",
            required: [ "Sid", "Participants"],
            properties: {
                Sid: {
                    bsonType: "string",
                    description: "must be a string and required"
                },
                Participants: {
                    bsonType: "array",
                    description: "must be an array of objects and required"
                },
                CreateTime: {
                    bsonType: "date",
                    description: "must be a date and required"
                },
                UpdateTime: {
                    bsonType: "date",
                    description: "must be a date and required"
                }                
            }
        }
    },
    validationAction: "warn"
});

// Notes collection
db.createCollection("Notes", {
    validator: {
        $jsonSchema: {
            bsonType: "object",
            required: [ "UserId", "ContributionId", "ClassId"],
            properties: {
                UserId: {
                    bsonType: "string",
                    description: "must be a string and required"
                },
                ContributionId: {
                    bsonType: "string",
                    description: "must be a string and required"
                },
                ClassId: {
                    bsonType: "string",
                    description: "must be a string and required"
                },
                Text: {
                    bsonType: "string",
                    description: "must be a string"
                },
                CreateTime: {
                    bsonType: "date",
                    description: "must be a date and required"
                },
                UpdateTime: {
                    bsonType: "date",
                    description: "must be a date and required"
                }                
            }
        }
    },
    validationAction: "warn"
});

// Agreements collection
db.createCollection("Agreements", {
    validator: {
        $jsonSchema: {
            bsonType: "object",
            required: [ "AgreementType", "FileUrl", "FileNameWithExtension", "IsLatest"],
            properties: {
                AgreementType: {
                    bsonType: "string",
                    description: "must be a string and required"
                },
                FileUrl: {
                    bsonType: "string",
                    description: "must be a string and required"
                },
                FileNameWithExtension: {
                    bsonType: "string",
                    description: "must be a string and required"
                },
                IsLatest: {
                    bsonType: "bool",
                    description: "must be a bool and required"
                },
                CreateTime: {
                    bsonType: "date",
                    description: "must be a date and required"
                },
                UpdateTime: {
                    bsonType: "date",
                    description: "must be a date and required"
                }                
            }
        }
    },
    validationAction: "warn"
});

db.Agreements.insertMany([
    {
        _id: new ObjectId().str,
        AgreementType: "User",
        FileUrl: "",
        FileNameWithExtension: "",
        IsLatest: true,
        CreateTime: new Date(),
        UpdateTime: new Date()
    },
    {
        _id: new ObjectId().str,
        AgreementType: "TermsOfUse",
        FileUrl: "",
        FileNameWithExtension: "",
        IsLatest: true,
        CreateTime: new Date(),
        UpdateTime: new Date()
    },
    {
        _id: new ObjectId().str,
        AgreementType: "Payment",
        FileUrl: "",
        FileNameWithExtension: "",
        IsLatest: true,
        CreateTime: new Date(),
        UpdateTime: new Date()
    },
    {
        _id: new ObjectId().str,
        AgreementType: "PrivacyPolicy",
        FileUrl: "",
        FileNameWithExtension: "",
        IsLatest: true,
        CreateTime: new Date(),
        UpdateTime: new Date()
    }
]);
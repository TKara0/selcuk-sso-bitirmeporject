// Selçuk tasarım sistemi: küçük, çerçevesiz iyileştirmeler. Sayfalar betik olmadan da çalışır; betik yalnızca
// data-* öznitelikli öğelere davranış ekler. Satır içi betik ve stil yok (CSP: default-src 'self').
(function () {
    "use strict";

    var canliBolge = null;

    function duyur(metin) {
        if (!canliBolge) {
            canliBolge = document.createElement("div");
            canliBolge.className = "gorunmez";
            canliBolge.setAttribute("aria-live", "polite");
            document.body.appendChild(canliBolge);
        }

        canliBolge.textContent = "";
        window.setTimeout(function () { canliBolge.textContent = metin; }, 50);
    }

    // Şifre göster/gizle: <button data-sifre-goster aria-controls="alan-id" aria-pressed="false" hidden>
    function sifreGoster(dugme) {
        var alan = document.getElementById(dugme.getAttribute("aria-controls"));
        if (!alan) {
            return;
        }

        dugme.hidden = false;
        dugme.addEventListener("click", function () {
            var goster = alan.type === "password";
            alan.type = goster ? "text" : "password";
            dugme.setAttribute("aria-pressed", goster ? "true" : "false");
        });
    }

    // Hesap menüsü: button[aria-expanded][aria-controls] + liste. Esc kapatır ve odağı düğmeye döndürür; dışarı
    // tıklayınca veya odak menüden çıkınca kapanır.
    function menu(kap) {
        var dugme = kap.querySelector("[data-menu-dugme]");
        var liste = document.getElementById(dugme.getAttribute("aria-controls"));

        function ac(acik) {
            dugme.setAttribute("aria-expanded", acik ? "true" : "false");
            liste.hidden = !acik;
        }

        dugme.addEventListener("click", function () {
            ac(dugme.getAttribute("aria-expanded") !== "true");
        });
        kap.addEventListener("keydown", function (olay) {
            if (olay.key === "Escape" && dugme.getAttribute("aria-expanded") === "true") {
                ac(false);
                dugme.focus();
            }
        });
        kap.addEventListener("focusout", function (olay) {
            if (olay.relatedTarget && !kap.contains(olay.relatedTarget)) {
                ac(false);
            }
        });
        document.addEventListener("click", function (olay) {
            if (!kap.contains(olay.target)) {
                ac(false);
            }
        });
    }

    function kaynakMetni(secici, bosluksuz) {
        var kaynak = document.querySelector(secici);
        if (!kaynak) {
            return "";
        }

        var ogeler = kaynak.querySelectorAll("li");
        var metin = ogeler.length
            ? Array.prototype.map.call(ogeler, function (li) { return li.textContent.trim(); }).join("\n")
            : kaynak.textContent.trim();
        return bosluksuz ? metin.replace(/\s+/g, "") : metin;
    }

    // Kopyala: <button data-kopyala="#kaynak" data-kopyala-mesaj="…" [data-bosluksuz] hidden>
    function kopyala(dugme) {
        if (!navigator.clipboard) {
            return;
        }

        var etiket = dugme.querySelector("[data-etiket]") || dugme;
        var ilkMetin = etiket.textContent;
        dugme.hidden = false;
        dugme.addEventListener("click", function () {
            var metin = kaynakMetni(dugme.getAttribute("data-kopyala"), dugme.hasAttribute("data-bosluksuz"));
            navigator.clipboard.writeText(metin).then(function () {
                etiket.textContent = "Kopyalandı";
                duyur(dugme.getAttribute("data-kopyala-mesaj") || "Kopyalandı.");
                window.setTimeout(function () { etiket.textContent = ilkMetin; }, 2000);
            }, function () {
                duyur("Kopyalanamadı. Metni seçip elle kopyala.");
            });
        });
    }

    // Metin dosyası olarak indir: <button data-indir="#kaynak" data-dosya-adi="…" data-dosya-basligi="…" hidden>
    function indir(dugme) {
        if (!window.Blob || !window.URL || !URL.createObjectURL) {
            return;
        }

        dugme.hidden = false;
        dugme.addEventListener("click", function () {
            var baslik = dugme.getAttribute("data-dosya-basligi");
            var metin = (baslik ? baslik + "\r\n\r\n" : "") +
                kaynakMetni(dugme.getAttribute("data-indir"), false).replace(/\n/g, "\r\n") + "\r\n";
            var adres = URL.createObjectURL(new Blob([metin], { type: "text/plain;charset=utf-8" }));
            var baglanti = document.createElement("a");
            baglanti.href = adres;
            baglanti.download = dugme.getAttribute("data-dosya-adi") || "kodlar.txt";
            document.body.appendChild(baglanti);
            baglanti.click();
            baglanti.remove();
            window.setTimeout(function () { URL.revokeObjectURL(adres); }, 1000);
            duyur("Dosya indirildi.");
        });
    }

    // Kod alanı: yapıştırılan veya yazılan boşluklar temizlenir (sunucu da temizler).
    function kodAlani(alan) {
        alan.addEventListener("paste", function (olay) {
            var veri = olay.clipboardData && olay.clipboardData.getData("text");
            if (typeof veri !== "string") {
                return;
            }

            olay.preventDefault();
            var temiz = veri.replace(/\s+/g, "");
            var bas = alan.selectionStart, son = alan.selectionEnd;
            alan.value = alan.value.slice(0, bas) + temiz + alan.value.slice(son);
            alan.setSelectionRange(bas + temiz.length, bas + temiz.length);
        });
        alan.addEventListener("input", function () {
            if (/\s/.test(alan.value)) {
                var konum = alan.selectionStart - (alan.value.slice(0, alan.selectionStart).match(/\s/g) || []).length;
                alan.value = alan.value.replace(/\s+/g, "");
                alan.setSelectionRange(konum, konum);
            }
        });
    }

    // Kurtarma kodu geçişi: aynı alan iki biçimde; etiket, ipucu, yer tutucu, klavye türü ve düğme metni değişir.
    // Boş ipucu gizlenir. <button data-kod-modu="alan-id" data-etiket-id data-ipucu-id data-*-kurtarma hidden>
    function kodModu(dugme) {
        var alan = document.getElementById(dugme.getAttribute("data-kod-modu"));
        var etiket = document.getElementById(dugme.getAttribute("data-etiket-id"));
        var ipucu = document.getElementById(dugme.getAttribute("data-ipucu-id"));
        if (!alan || !etiket || !ipucu) {
            return;
        }

        var modlar = [
            {
                etiket: etiket.textContent, ipucu: ipucu.textContent, dugme: dugme.textContent,
                yerTutucu: alan.getAttribute("placeholder") || "", klavye: "numeric", otomatik: "one-time-code"
            },
            {
                etiket: dugme.getAttribute("data-etiket-kurtarma"), ipucu: dugme.getAttribute("data-ipucu-kurtarma"),
                dugme: dugme.getAttribute("data-dugme-kurtarma"),
                yerTutucu: dugme.getAttribute("data-yer-tutucu-kurtarma") || "", klavye: "text", otomatik: "off"
            }
        ];
        var sira = 0;
        dugme.hidden = false;
        dugme.addEventListener("click", function () {
            sira = 1 - sira;
            var mod = modlar[sira];
            etiket.textContent = mod.etiket;
            ipucu.textContent = mod.ipucu;
            ipucu.hidden = !mod.ipucu;
            dugme.textContent = mod.dugme;
            alan.setAttribute("placeholder", mod.yerTutucu);
            alan.setAttribute("inputmode", mod.klavye);
            alan.setAttribute("autocomplete", mod.otomatik);
            alan.value = "";
            alan.focus();
        });
    }

    // Şifre politikası: kurallar yazarken denetlenir; yalnızca durumu değişen madde güncellenir (aria-live polite).
    // <ul data-sifre-politikasi="alan-id"><li data-kural="uzunluk" data-deger="10">…<span data-durum></span></li>
    var kurallar = {
        uzunluk: function (s, d) { return s.length >= d; },
        buyuk: function (s) { return /\p{Lu}/u.test(s); },
        kucuk: function (s) { return /\p{Ll}/u.test(s); },
        rakam: function (s) { return /\p{Nd}/u.test(s); },
        sembol: function (s) { return /[^\p{L}\p{Nd}]/u.test(s); },
        farkli: function (s, d) { return new Set(Array.from(s)).size >= d; }
    };

    function sifrePolitikasi(liste) {
        var alan = document.getElementById(liste.getAttribute("data-sifre-politikasi"));
        if (!alan) {
            return;
        }

        var maddeler = Array.prototype.slice.call(liste.querySelectorAll("[data-kural]"));
        function denetle() {
            maddeler.forEach(function (madde) {
                var kural = kurallar[madde.getAttribute("data-kural")];
                if (!kural) {
                    return;
                }

                // Başlangıçta hiçbiri karşılanmamış sayılır; yalnızca durum değişince duyurulur.
                var karsilandi = kural(alan.value, parseInt(madde.getAttribute("data-deger") || "0", 10));
                if (madde.classList.contains("karsilandi") !== karsilandi) {
                    madde.classList.toggle("karsilandi", karsilandi);
                    madde.querySelector("[data-durum]").textContent = karsilandi ? "karşılandı" : "karşılanmadı";
                }
            });
        }

        alan.addEventListener("input", denetle);
    }

    // İşlem sonucu bandı: sayfa yüklendikten sonra canlı bölgeye yazılır (yükleme anında var olan metin duyurulmaz).
    // <div data-duyur>…</div>
    function duyuru(oge) {
        duyur(oge.textContent.replace(/\s+/g, " ").trim());
    }

    function hepsi(secici, islev) {
        Array.prototype.forEach.call(document.querySelectorAll(secici), islev);
    }

    hepsi("[data-sifre-goster]", sifreGoster);
    hepsi("[data-menu]", menu);
    hepsi("[data-kopyala]", kopyala);
    hepsi("[data-indir]", indir);
    hepsi("[data-kod-alani]", kodAlani);
    hepsi("[data-kod-modu]", kodModu);
    hepsi("[data-sifre-politikasi]", sifrePolitikasi);
    hepsi("[data-duyur]", duyuru);
})();
